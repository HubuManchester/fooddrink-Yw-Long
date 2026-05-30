using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;
using WhatToEat.Services;

namespace WhatToEat.ViewModels
{
    /// <summary>
    /// ViewModel for HomePage.
    /// Hardware features used:
    ///   1. Text-to-Speech  — SpeakCommand reads selected meal aloud.
    ///   2. Accelerometer / Shake — ShakeToSpin: shaking the device triggers the spin wheel.
    ///      Uses MAUI's built-in Accelerometer API to detect shake gestures.
    ///      Falls back gracefully if the accelerometer is not available.
    /// </summary>
    public class HomeViewModel : INotifyPropertyChanged
    {
        private readonly MealService _mealService;

        // ── Shake / Accelerometer ────────────────────────────────────────
        // Threshold: total acceleration magnitude above this value triggers a shake.
        // 2.5 G is a comfortable shake without being too sensitive.
        private const double ShakeThreshold = 2.5;
        private DateTime _lastShakeTime = DateTime.MinValue;
        private const int ShakeCooldownMs = 2000; // prevent rapid re-triggering

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
            SpeakCommand = new Command(async () => await SpeakSelectedAsync());
            ConfirmSelectionCommand = new Command(
                async () => await Shell.Current.GoToAsync($"//Discover?mealId={SelectedMeal?.Id}"),
                () => HasSelectedMeal);

            SelectMealCommand = new Command<Meal>(meal =>
            {
                if (meal == null) return;

                // Deselect previous meal
                if (_selectedMeal != null)
                    _selectedMeal.IsSelected = false;

                // Select new meal
                SelectedMeal = meal;
                meal.IsSelected = true;

                (ConfirmSelectionCommand as Command)?.ChangeCanExecute();
            });

            // Start accelerometer when ViewModel is created
            StartAccelerometer();
        }

        // ── Accelerometer / Shake detection ──────────────────────────────

        /// <summary>
        /// Starts the accelerometer sensor.
        /// Catches FeatureNotSupportedException on platforms that lack the sensor
        /// (e.g. Windows emulator), so the app still runs correctly.
        /// Hardware feature: Accelerometer (Shake).
        /// </summary>
        private void StartAccelerometer()
        {
            try
            {
                if (!Accelerometer.Default.IsSupported)
                {
                    // Sensor not available (e.g. Windows desktop) — fail silently
                    ShakeHintText = "Shake not available on this device";
                    return;
                }

                if (Accelerometer.Default.IsMonitoring)
                    return; // already running

                Accelerometer.Default.ReadingChanged += OnAccelerometerReadingChanged;
                Accelerometer.Default.Start(SensorSpeed.Game);

                ShakeHintText = "Shake your phone to spin the wheel!";
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

        /// <summary>
        /// Stops the accelerometer when the page is no longer visible
        /// to save battery and avoid background processing.
        /// Call this from the page's OnDisappearing.
        /// </summary>
        public void StopAccelerometer()
        {
            try
            {
                if (Accelerometer.Default.IsMonitoring)
                {
                    Accelerometer.Default.Stop();
                    Accelerometer.Default.ReadingChanged -= OnAccelerometerReadingChanged;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HomeViewModel] Accelerometer stop error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called whenever the accelerometer reports a new reading.
        /// Computes the magnitude of total acceleration and triggers SpinAsync
        /// if it exceeds ShakeThreshold and the cooldown has elapsed.
        /// </summary>
        private void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
        {
            var data = e.Reading;

            // Magnitude of the acceleration vector (gravity ≈ 1 G at rest)
            double magnitude = Math.Sqrt(
                data.Acceleration.X * data.Acceleration.X +
                data.Acceleration.Y * data.Acceleration.Y +
                data.Acceleration.Z * data.Acceleration.Z);

            // Check threshold and cooldown
            bool cooldownElapsed =
                (DateTime.UtcNow - _lastShakeTime).TotalMilliseconds > ShakeCooldownMs;

            if (magnitude > ShakeThreshold && cooldownElapsed && !IsBusy)
            {
                _lastShakeTime = DateTime.UtcNow;

                // Accelerometer events fire on a background thread — marshal to UI thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await SpinAsync();
                });
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

        // ── Properties ───────────────────────────────────────────────────

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); }
        }
        public bool IsNotBusy => !_isBusy;

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

        private string _statusInfo = "Spin the wheel to discover food!";
        public string StatusInfo
        {
            get => _statusInfo;
            set => Set(ref _statusInfo, value);
        }

        /// <summary>
        /// Hint shown below the wheel. Updated based on accelerometer availability.
        /// </summary>
        private string _shakeHintText = "Shake your phone to spin the wheel!";
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

        private int _currentCategoryIndex = 0;

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand SpinCommand { get; }
        public ICommand ConfirmCategoryCommand { get; }
        public ICommand SpeakCommand { get; }
        public ICommand ConfirmSelectionCommand { get; }
        public ICommand SelectMealCommand { get; }

        // ── Spin ─────────────────────────────────────────────────────────

        private async Task SpinAsync()
        {
            if (IsBusy) return;
            ErrorMessage = string.Empty;

            var rng = new Random();
            _currentCategoryIndex = rng.Next(Categories.Length);
            SpinRequested?.Invoke(this, _currentCategoryIndex);

            await Task.Delay(2300);

            var winner = Categories[_currentCategoryIndex];
            SpinResultText = winner.Display;
            HasSpunOnce = true;

            (ConfirmCategoryCommand as Command)?.ChangeCanExecute();
        }

        // ── Fetch recommendations ─────────────────────────────────────────

        private async Task FetchRecommendationsAsync()
        {
            if (!HasSpunOnce)
            {
                ErrorMessage = "Please spin the wheel first!";
                return;
            }

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

                // Auto-select first meal
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

        // ── TTS — Hardware feature ────────────────────────────────────────

        private async Task SpeakSelectedAsync()
        {
            if (SelectedMeal == null)
            {
                ErrorMessage = "Please select a meal first!";
                return;
            }

            try
            {
                var text = $"We recommend {SelectedMeal.StrMeal}. " +
                           $"{SelectedMeal.StrArea}.";

                await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions
                {
                    Pitch = 1.0f,
                    Volume = 1.0f
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Speech error: {ex.Message}";
            }
        }
    }
}