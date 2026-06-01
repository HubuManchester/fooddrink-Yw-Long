using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;
using WhatToEat.Services;

namespace WhatToEat.ViewModels
{
    public class HomeViewModel : INotifyPropertyChanged
    {
        private readonly MealService _mealService;

        private const double ShakeThreshold = 2.5;
        private DateTime _lastShakeTime = DateTime.MinValue;
        private const int ShakeCooldownMs = 2000;

        private CancellationTokenSource? _ttsCancellationTokenSource;

        public event EventHandler<int>? SpinRequested;

        public static readonly (string Display, string Emoji, string SearchKey)[] Categories =
        {
            ("Burger",  "🍔", "burger"),
            ("Noodles", "🍜", "noodles"),
            ("Bento",   "🍱", "bento"),
            ("Tacos",   "🌮", "tacos"),
            ("Pizza",   "🍕", "pizza"),
            ("Sushi",   "🍣", "sushi"),
            ("Curry",   "🍛", "curry"),
            ("Salad",   "🥗", "salad"),
        };

        public HomeViewModel()
        {
            _mealService = new MealService();
            RecommendedMeals = new ObservableCollection<Meal>();

            SpinCommand = new Command(async () => await SpinAsync());

            ConfirmCategoryCommand = new Command(
                async () => await FetchRecommendationsAsync(),
                () => HasSpunOnce && !IsBusy);

            RefreshCommand = new Command(async () => await ResetAsync());

            SpeakCommand = new Command(async () => await ToggleSpeakAsync());

            ConfirmSelectionCommand = new Command(
                async () => await Shell.Current.GoToAsync($"//Discover?mealId={SelectedMeal?.Id}"),
                () => HasSelectedMeal);

            SelectMealCommand = new Command<Meal>(meal =>
            {
                if (meal == null) return;
                if (_selectedMeal != null) _selectedMeal.IsSelected = false;
                SelectedMeal = meal;
                meal.IsSelected = true;
                (ConfirmSelectionCommand as Command)?.ChangeCanExecute();
            });

            StartAccelerometer();
        }

        // ── Accelerometer ─────────────────────────────────────────────────

        private void StartAccelerometer()
        {
            try
            {
                if (!Accelerometer.Default.IsSupported)
                {
                    ShakeHintText = "Shake not available on this device";
                    return;
                }
                if (Accelerometer.Default.IsMonitoring) return;

                Accelerometer.Default.ReadingChanged += OnAccelerometerReadingChanged;
                Accelerometer.Default.Start(SensorSpeed.Game);
                ShakeHintText = "Shake your phone to spin the wheel";
            }
            catch (FeatureNotSupportedException)
            {
                ShakeHintText = "Shake not supported on this device";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HomeViewModel] Accelerometer start error: {ex.Message}");
                ShakeHintText = string.Empty;
            }
        }

        public void StopAccelerometer()
        {
            try
            {
                if (!Accelerometer.Default.IsMonitoring) return;
                Accelerometer.Default.Stop();
                Accelerometer.Default.ReadingChanged -= OnAccelerometerReadingChanged;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HomeViewModel] Accelerometer stop error: {ex.Message}");
            }
        }

        private void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
        {
            var data = e.Reading;
            double magnitude = Math.Sqrt(
                data.Acceleration.X * data.Acceleration.X +
                data.Acceleration.Y * data.Acceleration.Y +
                data.Acceleration.Z * data.Acceleration.Z);

            bool cooldownElapsed =
                (DateTime.UtcNow - _lastShakeTime).TotalMilliseconds > ShakeCooldownMs;

            if (magnitude > ShakeThreshold && cooldownElapsed && !IsBusy)
            {
                _lastShakeTime = DateTime.UtcNow;
                MainThread.BeginInvokeOnMainThread(async () => await SpinAsync());
            }
        }

        // ── Properties ────────────────────────────────────────────────────

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); }
        }
        public bool IsNotBusy => !_isBusy;

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => Set(ref _isRefreshing, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { Set(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        private string _spinResultText = "Spin the wheel to start";
        public string SpinResultText
        {
            get => _spinResultText;
            set => Set(ref _spinResultText, value);
        }

        private string _statusInfo = "Spin the wheel to discover food";
        public string StatusInfo
        {
            get => _statusInfo;
            set => Set(ref _statusInfo, value);
        }

        private string _shakeHintText = "Shake your phone to spin the wheel";
        public string ShakeHintText
        {
            get => _shakeHintText;
            set => Set(ref _shakeHintText, value);
        }

        private bool _hasSpunOnce;
        public bool HasSpunOnce
        {
            get => _hasSpunOnce;
            set => Set(ref _hasSpunOnce, value);
        }

        private bool _hasRecommendations;
        public bool HasRecommendations
        {
            get => _hasRecommendations;
            set => Set(ref _hasRecommendations, value);
        }

        public ObservableCollection<Meal> RecommendedMeals { get; }

        private Meal? _selectedMeal;
        public Meal? SelectedMeal
        {
            get => _selectedMeal;
            set
            {
                if (Set(ref _selectedMeal, value))
                    OnPropertyChanged(nameof(HasSelectedMeal));
            }
        }
        public bool HasSelectedMeal => _selectedMeal != null;

        private bool _isSpeaking;
        public bool IsSpeaking
        {
            get => _isSpeaking;
            set { Set(ref _isSpeaking, value); OnPropertyChanged(nameof(SpeakButtonText)); }
        }
        public string SpeakButtonText => _isSpeaking ? "Pause" : "Read Aloud";

        private int _currentCategoryIndex;

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand SpinCommand { get; }
        public ICommand ConfirmCategoryCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SpeakCommand { get; }
        public ICommand ConfirmSelectionCommand { get; }
        public ICommand SelectMealCommand { get; }

        // ── Spin ──────────────────────────────────────────────────────────

        private async Task SpinAsync()
        {
            if (IsBusy) return;
            ErrorMessage = string.Empty;

            var rng = new Random();
            _currentCategoryIndex = rng.Next(Categories.Length);
            SpinRequested?.Invoke(this, _currentCategoryIndex);

            await Task.Delay(2300);

            SpinResultText = Categories[_currentCategoryIndex].Display;
            HasSpunOnce = true;
            (ConfirmCategoryCommand as Command)?.ChangeCanExecute();
        }

        // ── Reset─────────────────────────────

        private Task ResetAsync()
        {
            CancelSpeaking();

            if (_selectedMeal != null) _selectedMeal.IsSelected = false;
            SelectedMeal = null;
            RecommendedMeals.Clear();

            HasSpunOnce = false;
            HasRecommendations = false;
            ErrorMessage = string.Empty;
            SpinResultText = "Spin the wheel to start";
            StatusInfo = "Spin the wheel to discover food";

            (ConfirmCategoryCommand as Command)?.ChangeCanExecute();
            (ConfirmSelectionCommand as Command)?.ChangeCanExecute();

            IsRefreshing = false;

            return Task.CompletedTask;
        }

        // ── Fetch recommendations ─────────────────────────────────────────

        private async Task FetchRecommendationsAsync()
        {
            if (!HasSpunOnce) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var category = Categories[_currentCategoryIndex];
                StatusInfo = $"Finding {category.Display} dishes for you...";

                var meals = await _mealService.SearchMealsByNameAsync(category.SearchKey);
                if (meals.Count == 0)
                {
                    var random = await _mealService.GetRandomMealAsync();
                    if (random != null) meals.Add(random);
                }

                if (meals.Count == 0)
                {
                    ErrorMessage = "No results found. Please check your connection.";
                    return;
                }

                RecommendedMeals.Clear();
                foreach (var meal in meals.Take(8))
                    RecommendedMeals.Add(meal);

                HasRecommendations = true;

                var firstMeal = RecommendedMeals.First();
                firstMeal.IsSelected = true;
                SelectedMeal = firstMeal;

                StatusInfo = $"Top {category.Display} picks for you";
                (ConfirmSelectionCommand as Command)?.ChangeCanExecute();
            }
            catch (HttpRequestException)
            {
                ErrorMessage = "Network error. Please check your connection.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                Console.WriteLine($"[HomeViewModel] Error: {ex}");
            }
            finally
            {
                IsBusy = false;
                (ConfirmCategoryCommand as Command)?.ChangeCanExecute();
            }
        }

        // ── TTS toggle play / pause ───────────────────────────────────────

        private async Task ToggleSpeakAsync()
        {
            if (_isSpeaking) { CancelSpeaking(); return; }

            if (SelectedMeal == null)
            {
                ErrorMessage = "Please select a meal first!";
                return;
            }

            await StartSpeakingAsync();
        }

        private async Task StartSpeakingAsync()
        {
            if (SelectedMeal == null) return;

            CancelSpeaking();
            _ttsCancellationTokenSource = new CancellationTokenSource();

            try
            {
                IsSpeaking = true;
                var text = $"We recommend {SelectedMeal.StrMeal}. {SelectedMeal.StrArea}.";
                await TextToSpeech.Default.SpeakAsync(
                    text,
                    new SpeechOptions { Pitch = 1.0f, Volume = 1.0f },
                    cancelToken: _ttsCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[HomeViewModel] Speech was cancelled by user.");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Speech error: {ex.Message}";
                Console.WriteLine($"[HomeViewModel] Speech error: {ex}");
            }
            finally
            {
                IsSpeaking = false;
                _ttsCancellationTokenSource?.Dispose();
                _ttsCancellationTokenSource = null;
            }
        }

        private void CancelSpeaking()
        {
            if (_ttsCancellationTokenSource != null &&
                !_ttsCancellationTokenSource.IsCancellationRequested)
                _ttsCancellationTokenSource.Cancel();
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
    }
}