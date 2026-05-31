using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;
using WhatToEat.Services;

namespace WhatToEat.ViewModels
{
    /// <summary>
    /// ViewModel for DiscoverPage.
    /// Receives mealId via Shell query parameter (?mealId=xxx).
    /// Hardware features:
    ///   1. Text-to-Speech — ReadAloudCommand reads recipe aloud (toggle play/pause)
    /// Manual search via text input replaces voice search.
    /// </summary>
    public class DiscoverViewModel : INotifyPropertyChanged, IQueryAttributable
    {
        private readonly MealService _mealService = new();
        private int _lastMealId;

        // CancellationTokenSource for TTS cancellation
        private CancellationTokenSource? _ttsCancellationTokenSource;

        public DiscoverViewModel()
        {
            // Toggle TTS play/pause on each tap
            ReadAloudCommand = new Command(
                async () => await ToggleReadAloudAsync(),
                () => Recipe != null && !IsBusy);

            RetryCommand = new Command(
                async () => await LoadRecipeAsync(_lastMealId),
                () => !IsBusy);

            // Text search command — triggered by the search button
            TextSearchCommand = new Command(
                async () => await TextSearchAsync(),
                () => !IsBusy && !string.IsNullOrWhiteSpace(SearchText));
        }

        // ── IQueryAttributable ────────────────────────────────────────────
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("mealId", out var raw)
                && int.TryParse(raw?.ToString(), out var id))
            {
                _ = LoadRecipeAsync(id);
            }
        }

        // ── Properties ────────────────────────────────────────────────────
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

        // TTS play/pause state
        private bool _isSpeaking;
        public bool IsSpeaking
        {
            get => _isSpeaking;
            set
            {
                Set(ref _isSpeaking, value);
                OnPropertyChanged(nameof(ReadAloudButtonText));
            }
        }

        // Button label toggles between Read Aloud and Stop
        public string ReadAloudButtonText => _isSpeaking ? "Stop" : "Read Aloud";

        // ── Text search ───────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                Set(ref _searchText, value);
                // Refresh CanExecute so button enables/disables as user types
                (TextSearchCommand as Command)?.ChangeCanExecute();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand ReadAloudCommand { get; }
        public ICommand RetryCommand { get; }
        public ICommand TextSearchCommand { get; }

        // ── Load recipe by ID (from HomePage) ────────────────────────────
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

        // ── Text search ───────────────────────────────────────────────────
        /// <summary>
        /// Searches Spoonacular for a recipe matching the user's typed query
        /// and loads the first result's full detail.
        /// </summary>
        private async Task TextSearchAsync()
        {
            var query = SearchText.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            Recipe = null;

            try
            {
                var found = await _mealService.SearchAndGetFirstDetailAsync(query);
                if (found != null)
                {
                    Recipe = found;
                    _lastMealId = found.Id;
                    SearchText = string.Empty; // clear input after success
                }
                else
                {
                    ErrorMessage = $"No recipes found for \"{query}\". Try another keyword.";
                }
            }
            catch (HttpRequestException)
            {
                ErrorMessage = "Network error. Please check your connection.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Search error: {ex.Message}";
                Console.WriteLine($"[DiscoverViewModel] TextSearch error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── TTS — Hardware feature 1 (Toggle play/pause) ──────────────────────

        /// <summary>
        /// Toggles between speaking the recipe and stopping the speech.
        /// First tap → speaks the recipe details.
        /// Second tap → cancels speech immediately.
        /// </summary>
        private async Task ToggleReadAloudAsync()
        {
            if (_isSpeaking)
            {
                // Cancel current speech
                CancelSpeaking();
                return;
            }

            if (Recipe == null) return;
            await StartSpeakingAsync();
        }

        /// <summary>
        /// Starts reading the recipe aloud
        /// </summary>
        private async Task StartSpeakingAsync()
        {
            if (Recipe == null) return;

            // Cancel any ongoing speech
            CancelSpeaking();

            // Create new CancellationTokenSource
            _ttsCancellationTokenSource = new CancellationTokenSource();

            try
            {
                IsSpeaking = true;

                var ingredientList = string.Join(", ",
                    Recipe.ExtendedIngredients.Take(5).Select(i => i.Name));

                var stepText = string.Join(" ",
                    Recipe.Steps.Take(3).Select(s => $"Step {s.Number}. {s.Step}"));

                var script = $"{Recipe.Title}. " +
                             $"Ready in {Recipe.ReadyInMinutes} minutes, " +
                             $"serves {Recipe.Servings}. " +
                             $"You will need: {ingredientList}. " +
                             $"Here are the first steps. {stepText}";

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await TextToSpeech.Default.SpeakAsync(
                        script,
                        new SpeechOptions
                        {
                            Pitch = 1.0f,
                            Volume = 1.0f
                        },
                        cancelToken: _ttsCancellationTokenSource!.Token);
                });
            }
            catch (OperationCanceledException)
            {
                // User cancelled speech, this is expected behavior
                Console.WriteLine("[DiscoverViewModel] Speech was cancelled by user.");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Speech error: {ex.Message}";
                Console.WriteLine($"[DiscoverViewModel] Speech error: {ex}");
            }
            finally
            {
                // Reset state after speech finishes (whether completed or cancelled)
                IsSpeaking = false;
                _ttsCancellationTokenSource?.Dispose();
                _ttsCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Cancels the current speech if any is in progress
        /// </summary>
        private void CancelSpeaking()
        {
            if (_ttsCancellationTokenSource != null && !_ttsCancellationTokenSource.IsCancellationRequested)
            {
                _ttsCancellationTokenSource.Cancel();
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
            (RetryCommand as Command)?.ChangeCanExecute();
            (TextSearchCommand as Command)?.ChangeCanExecute();
        }
    }
}