using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;
using WhatToEat.Services;
using CommunityToolkit.Maui.Media;

namespace WhatToEat.ViewModels
{
    /// <summary>
    /// ViewModel for DiscoverPage.
    /// Receives mealId via Shell query parameter (?mealId=xxx).
    /// Hardware features:
    ///   1. Text-to-Speech  — ReadAloudCommand
    ///   2. Microphone/STT  — VoiceSearchCommand
    ///      Requires: CommunityToolkit.Maui NuGet package
    ///      MauiProgram.cs: .UseMauiCommunityToolkit()
    ///      AndroidManifest: RECORD_AUDIO permission
    /// </summary>
    public class DiscoverViewModel : INotifyPropertyChanged, IQueryAttributable
    {
        private readonly MealService _mealService = new();
        private int _lastMealId;

        public DiscoverViewModel()
        {
            ReadAloudCommand = new Command(async () => await ReadAloudAsync(),
                                             () => Recipe != null && !IsBusy);
            VoiceSearchCommand = new Command(async () => await VoiceSearchAsync(),
                                             () => !IsListening && !IsBusy);
            RetryCommand = new Command(async () => await LoadRecipeAsync(_lastMealId),
                                             () => !IsBusy);
        }

        // ── IQueryAttributable ───────────────────────────────────────────
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("mealId", out var raw)
                && int.TryParse(raw?.ToString(), out var id))
            {
                _ = LoadRecipeAsync(id);
            }
        }

        // ── Properties ───────────────────────────────────────────────────
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                Set(ref _isBusy, value);
                OnPropertyChanged(nameof(IsNotBusy));
                RefreshCanExecute();
            }
        }
        public bool IsNotBusy => !_isBusy;

        private bool _isListening;
        public bool IsListening
        {
            get => _isListening;
            set
            {
                Set(ref _isListening, value);
                OnPropertyChanged(nameof(IsNotListening));
                RefreshCanExecute();
            }
        }
        public bool IsNotListening => !_isListening;

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { Set(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        private RecipeDetail? _recipe;
        public RecipeDetail? Recipe
        {
            get => _recipe;
            set
            {
                Set(ref _recipe, value);
                OnPropertyChanged(nameof(HasRecipe));
                OnPropertyChanged(nameof(TimeLabel));
                OnPropertyChanged(nameof(ServingsLabel));
                RefreshCanExecute();
            }
        }
        public bool HasRecipe => _recipe != null;
        public string TimeLabel => Recipe != null ? $"{Recipe.ReadyInMinutes} min" : "--";
        public string ServingsLabel => Recipe != null ? $"{Recipe.Servings} servings" : "--";

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand ReadAloudCommand { get; }
        public ICommand VoiceSearchCommand { get; }
        public ICommand RetryCommand { get; }

        // ── Load recipe detail ───────────────────────────────────────────
        private async Task LoadRecipeAsync(int mealId)
        {
            if (mealId <= 0) return;
            _lastMealId = mealId;
            IsBusy = true;
            ErrorMessage = string.Empty;
            Recipe = null;

            try
            {
                var detail = await _mealService.GetRecipeDetailAsync(mealId);
                if (detail == null)
                {
                    ErrorMessage = "Could not load recipe details. Please try again.";
                    return;
                }
                Recipe = detail;
            }
            catch (HttpRequestException)
            {
                ErrorMessage = "Network error. Please check your connection.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unexpected error: {ex.Message}";
                Console.WriteLine($"[DiscoverViewModel] {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── TTS — Hardware feature 1 ─────────────────────────────────────
        private async Task ReadAloudAsync()
        {
            if (Recipe == null) return;

            try
            {
                var locales = await Task.Run(() => TextToSpeech.Default.GetLocalesAsync());
                var locale = locales.FirstOrDefault(l => l.Language.StartsWith("en"))
                             ?? locales.FirstOrDefault();

                if (locale == null)
                {
                    ErrorMessage = "No TTS language available on this device.";
                    return;
                }

                var ingredientList = string.Join(", ",
                    Recipe.ExtendedIngredients.Take(5).Select(i => i.Name));
                var stepText = string.Join(" ",
                    Recipe.Steps.Take(3).Select(s => $"Step {s.Number}. {s.Step}"));
                var script = $"{Recipe.Title}. " +
                             $"Ready in {Recipe.ReadyInMinutes} minutes, serves {Recipe.Servings}. " +
                             $"You will need: {ingredientList}. " +
                             $"Here are the first steps. {stepText}";

                await Task.Delay(300);

                // ✅ 传入 locale，指定英语
                await TextToSpeech.Default.SpeakAsync(script, new SpeechOptions
                {
                    Pitch = 1.0f,
                    Volume = 1.0f,
                    Locale = locale
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Speech error: {ex.Message}";
            }
        }

        // ── Voice search — Hardware feature 2 (Microphone / STT) ─────────
        /// <summary>
        /// Listens to the user's spoken food query, searches Spoonacular,
        /// and loads the first matching recipe.
        /// Requires CommunityToolkit.Maui — see class summary for setup.
        /// </summary>
        private async Task VoiceSearchAsync()
        {
            try
            {
                IsListening = true;
                ErrorMessage = string.Empty;

                var status = await Permissions.RequestAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted)
                {
                    ErrorMessage = "Microphone permission is required for voice search.";
                    return;
                }

                // ✅ 用 CancellationToken 设置 10 秒超时
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                SpeechToTextResult result;
                try
                {
                    result = await SpeechToText.Default.ListenAsync(
                        System.Globalization.CultureInfo.GetCultureInfo("en-US"), // ✅ 强制用英语，不用 CurrentCulture
                        new Progress<string>(partial =>
                        {
                            // 有中间结果就直接用，不等最终结果
                            if (!string.IsNullOrWhiteSpace(partial))
                                MainThread.BeginInvokeOnMainThread(() =>
                                    ErrorMessage = $"Hearing: {partial}");
                        }),
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    ErrorMessage = "Listening timed out. Please try again.";
                    return;
                }

                // ✅ 检查 Text 而不只看 IsSuccessful，部分真机 IsSuccessful=false 但 Text 有值
                var recognisedText = result.Text?.Trim();
                if (string.IsNullOrWhiteSpace(recognisedText))
                {
                    ErrorMessage = "Could not understand. Please try again.";
                    return;
                }

                IsListening = false;
                IsBusy = true;

                var found = await _mealService.SearchAndGetFirstDetailAsync(recognisedText);
                if (found != null)
                {
                    Recipe = found;
                    _lastMealId = found.Id;
                    ErrorMessage = string.Empty;
                }
                else
                {
                    ErrorMessage = $"No recipes found for \"{recognisedText}\". Try another food name.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Voice search error: {ex.Message}";
            }
            finally
            {
                IsListening = false;
                IsBusy = false;
            }
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private void RefreshCanExecute()
        {
            (ReadAloudCommand as Command)?.ChangeCanExecute();
            (VoiceSearchCommand as Command)?.ChangeCanExecute();
            (RetryCommand as Command)?.ChangeCanExecute();
        }
    }
}