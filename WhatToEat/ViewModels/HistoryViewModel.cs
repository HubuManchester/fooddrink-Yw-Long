using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;
using WhatToEat.Services;

namespace WhatToEat.ViewModels
{
    /// <summary>
    /// ViewModel for HistoryPage.
    /// Maintains grouped meal records, statistics and page logic.
    /// Integrated hardware features: Haptic Feedback, Text-to-Speech.
    /// Data source: Local SQLite database via DatabaseService.
    /// </summary>
    public class HistoryViewModel : INotifyPropertyChanged
    {
        // CancellationTokenSource for TTS cancellation
        private CancellationTokenSource? _ttsCancellationTokenSource;

        /// <summary>
        /// Initialize commands and collection.
        /// </summary>
        public HistoryViewModel()
        {
            GroupedEntries = new ObservableCollection<MealDayGroup>();

            RefreshCommand = new Command(async () => await LoadHistoryAsync());
            DeleteCommand = new Command<MealLogEntry>(DeleteEntry, e => !IsBusy);
            ClearAllCommand = new Command(async () => await ClearAllAsync(), () => HasEntries && !IsBusy);
            ReadSummaryCommand = new Command(async () => await ToggleReadSummaryAsync(), () => HasEntries && !IsBusy);
            ToggleStatsCommand = new Command(ToggleStats);
        }

        /// <summary>
        /// Meal records grouped by calendar date.
        /// Bound to the detail list on UI.
        /// </summary>
        public ObservableCollection<MealDayGroup> GroupedEntries { get; }

        #region Busy State
        private bool _isBusy;
        /// <summary>
        /// Indicates if page is performing background work.
        /// Disables interactive controls when true.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); RefreshCanExecute(); }
        }

        /// <summary>
        /// Inverse of IsBusy for UI binding.
        /// </summary>
        public bool IsNotBusy => !_isBusy;
        #endregion

        #region Error Message
        private string _errorMessage = string.Empty;
        /// <summary>
        /// Page level error message.
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set { Set(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
        }

        /// <summary>
        /// Determine whether to show error UI.
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(_errorMessage);
        #endregion

        #region Record Existence State
        private bool _hasEntries;
        /// <summary>
        /// True when there is at least one meal record.
        /// </summary>
        public bool HasEntries
        {
            get => _hasEntries;
            set
            {
                Set(ref _hasEntries, value);
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsDetailsVisible));
                RefreshCanExecute();
            }
        }

        /// <summary>
        /// True when no meal records exist. Show empty state UI.
        /// </summary>
        public bool IsEmpty => !_hasEntries;
        #endregion

        #region Expand / Collapse Detail State
        private bool _isStatsExpanded;
        /// <summary>
        /// Whether the statistics card is expanded to reveal the detailed
        /// "what you ate" breakdown below it.
        /// </summary>
        public bool IsStatsExpanded
        {
            get => _isStatsExpanded;
            set
            {
                Set(ref _isStatsExpanded, value);
                OnPropertyChanged(nameof(StatsExpandIcon));
                OnPropertyChanged(nameof(StatsExpandHint));
                OnPropertyChanged(nameof(IsDetailsVisible));
            }
        }

        /// <summary>Arrow glyph indicating collapsed (▼) or expanded (▲) state.</summary>
        public string StatsExpandIcon => _isStatsExpanded ? "▲" : "▼";

        /// <summary>Hint text prompting the user to tap the card.</summary>
        public string StatsExpandHint => _isStatsExpanded
            ? "Tap to hide details"
            : "Tap to see what you ate";

        /// <summary>
        /// True only when records exist AND the card is expanded.
        /// Controls visibility of the detailed daily meal list.
        /// </summary>
        public bool IsDetailsVisible => _hasEntries && _isStatsExpanded;
        #endregion

        #region Statistics Properties
        private int _totalCaloriesAllTime;
        /// <summary>
        /// Sum calories of all saved meals.
        /// </summary>
        public int TotalCaloriesAllTime
        {
            get => _totalCaloriesAllTime;
            set { Set(ref _totalCaloriesAllTime, value); OnPropertyChanged(nameof(TotalCaloriesLabel)); }
        }
        /// <summary>
        /// Formatted text for total calories display.
        /// </summary>
        public string TotalCaloriesLabel => $"{_totalCaloriesAllTime} kcal";

        private int _todayCalories;
        /// <summary>
        /// Sum calories of meals logged today.
        /// </summary>
        public int TodayCalories
        {
            get => _todayCalories;
            set { Set(ref _todayCalories, value); OnPropertyChanged(nameof(TodayCaloriesLabel)); }
        }
        /// <summary>
        /// Formatted text for today's calories display.
        /// </summary>
        public string TodayCaloriesLabel => $"{_todayCalories} kcal";

        private int _totalMeals;
        /// <summary>
        /// Total number of all logged meals.
        /// </summary>
        public int TotalMeals
        {
            get => _totalMeals;
            set { Set(ref _totalMeals, value); OnPropertyChanged(nameof(TotalMealsLabel)); }
        }
        /// <summary>
        /// Formatted text for total meal count display.
        /// </summary>
        public string TotalMealsLabel => _totalMeals == 1 ? "1 meal" : $"{_totalMeals} meals";

        // TTS play/pause state
        private bool _isSpeaking;
        /// <summary>
        /// True while TTS is actively speaking the summary.
        /// </summary>
        public bool IsSpeaking
        {
            get => _isSpeaking;
            set
            {
                Set(ref _isSpeaking, value);
                OnPropertyChanged(nameof(ReadSummaryButtonText));
            }
        }

        /// <summary>
        /// Button label toggles between Read Summary and Stop.
        /// </summary>
        public string ReadSummaryButtonText => _isSpeaking ? "Stop" : "Read Summary";
        #endregion

        #region Commands
        /// <summary>
        /// Reload all meal records from database.
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// Delete single meal record.
        /// </summary>
        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Clear all meal records after confirmation.
        /// </summary>
        public ICommand ClearAllCommand { get; }

        /// <summary>
        /// Read meal summary via device Text-to-Speech (toggle play/pause).
        /// </summary>
        public ICommand ReadSummaryCommand { get; }

        /// <summary>
        /// Toggle expansion of the statistics card to show/hide meal details.
        /// </summary>
        public ICommand ToggleStatsCommand { get; }
        #endregion

        #region Expand / Collapse Logic
        /// <summary>
        /// Flip the expanded state and give light haptic feedback.
        /// </summary>
        private void ToggleStats()
        {
            IsStatsExpanded = !IsStatsExpanded;

            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
            catch
            {
                // Haptic not supported on this device — ignore silently.
            }
        }
        #endregion

        #region Load & Refresh Logic
        /// <summary>
        /// Fetch data from database, group records by date and calculate statistics.
        /// </summary>
        public async Task LoadHistoryAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                GroupedEntries.Clear();

                var allMeals = await DatabaseService.Instance.GetAllMealsAsync();

                if (allMeals.Count == 0)
                {
                    HasEntries = false;
                    TodayCalories = 0;
                    TotalCaloriesAllTime = 0;
                    TotalMeals = 0;
                    return;
                }

                // Group meals by date, sort from newest to oldest
                var groups = allMeals
                    .GroupBy(m => m.LoggedAt.Date)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new MealDayGroup(g.Key, g.OrderByDescending(m => m.LoggedAt)));

                foreach (var group in groups)
                    GroupedEntries.Add(group);

                // Calculate today's calories
                TodayCalories = GroupedEntries
                    .FirstOrDefault(g => g.Date == DateTime.Today)
                    ?.TotalCalories ?? 0;

                // Calculate overall statistics
                TotalCaloriesAllTime = allMeals.Sum(m => m.Calories);
                TotalMeals = allMeals.Count;
                HasEntries = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load history: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion

        #region Delete Single Record
        /// <summary>
        /// Remove selected meal from database and refresh list.
        /// Trigger haptic feedback on completion.
        /// </summary>
        private async void DeleteEntry(MealLogEntry? entry)
        {
            if (entry == null) return;

            try
            {
                await DatabaseService.Instance.DeleteMealAsync(entry);
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete record: {ex.Message}";
            }
        }
        #endregion

        #region Clear All Records
        /// <summary>
        /// Show confirmation dialog, then erase all meal data.
        /// Trigger long press haptic for destructive action.
        /// </summary>
        private async Task ClearAllAsync()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Clear History",
                "Are you sure you want to delete all meal records? This action cannot be undone.",
                "Confirm", "Cancel");

            if (!confirm) return;

            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            await DatabaseService.Instance.ClearAllMealsAsync();
            await LoadHistoryAsync();
        }
        #endregion

        #region Text-to-Speech Summary (Toggle play/pause)

        /// <summary>
        /// Toggles between reading the summary and stopping the speech.
        /// First tap → reads the summary aloud.
        /// Second tap → cancels speech immediately.
        /// </summary>
        private async Task ToggleReadSummaryAsync()
        {
            if (_isSpeaking)
            {
                // Cancel current speech
                CancelSpeaking();
                return;
            }

            await StartSpeakingSummaryAsync();
        }

        /// <summary>
        /// Starts reading the meal summary aloud
        /// </summary>
        private async Task StartSpeakingSummaryAsync()
        {
            // Cancel any ongoing speech
            CancelSpeaking();

            // Create new CancellationTokenSource
            _ttsCancellationTokenSource = new CancellationTokenSource();

            try
            {
                IsSpeaking = true;

                var locales = await Task.Run(() => TextToSpeech.Default.GetLocalesAsync());
                var targetLocale = locales.FirstOrDefault(l => l.Language.StartsWith("en"))
                                 ?? locales.FirstOrDefault();

                if (targetLocale == null)
                {
                    ErrorMessage = "No available speech language on this device.";
                    return;
                }

                string speechContent;
                var todayGroup = GroupedEntries.FirstOrDefault(g => g.Date == DateTime.Today);

                if (todayGroup != null && todayGroup.Count > 0)
                {
                    var mealList = string.Join(", ",
                        todayGroup.Select(m => $"{m.FoodName} at {m.LoggedAt:h:mm tt}"));
                    speechContent = $"Today you recorded {todayGroup.MealCountLabel}. " +
                                    $"{mealList}. " +
                                    $"Total calories for today is {todayGroup.TotalCaloriesLabel}.";
                }
                else
                {
                    speechContent = $"You have not recorded any meals today. " +
                                    $"In total you have {TotalMealsLabel} with {TotalCaloriesLabel} calories.";
                }

                await Task.Delay(300);

                await TextToSpeech.Default.SpeakAsync(
                    speechContent,
                    new SpeechOptions
                    {
                        Pitch = 1.0f,
                        Volume = 1.0f,
                        Locale = targetLocale
                    },
                    cancelToken: _ttsCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // User cancelled speech, this is expected behavior
                Console.WriteLine("[HistoryViewModel] Speech was cancelled by user.");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Speech service error: {ex.Message}";
                Console.WriteLine($"ReadSummary Error: {ex}");
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

        #endregion

        #region Helper Methods
        /// <summary>
        /// Re-evaluate command CanExecute status.
        /// </summary>
        private void RefreshCanExecute()
        {
            (DeleteCommand as Command)?.ChangeCanExecute();
            (ClearAllCommand as Command)?.ChangeCanExecute();
            (ReadSummaryCommand as Command)?.ChangeCanExecute();
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raise property changed event.
        /// </summary>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Set field value and trigger property change if value differs.
        /// </summary>
        private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}