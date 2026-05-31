using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;

namespace WhatToEat.Models
{
    /// <summary>
    /// Represents a group of meal log entries for a single calendar day.
    /// Used by CollectionView's IsGrouped feature on HistoryPage.
    /// Inherits List&lt;MealLogEntry&gt; so MAUI's grouping engine can iterate items directly.
    /// </summary>
    public class MealDayGroup : List<MealLogEntry>, INotifyPropertyChanged
    {
        private CancellationTokenSource? _ttsCancellationTokenSource;
        private bool _isSpeaking;

        // ── Group metadata ───────────────────────────────────────────────

        /// <summary>The calendar date this group represents (time stripped).</summary>
        public DateTime Date { get; }

        /// <summary>
        /// Human-friendly label: "Today", "Yesterday", or "Mon, Apr 7".
        /// </summary>
        public string DateLabel { get; }

        /// <summary>Sum of all logged calories for this day.</summary>
        public int TotalCalories => this.Sum(e => e.Calories);

        /// <summary>Formatted calorie total displayed in the group header.</summary>
        public string TotalCaloriesLabel => $"{TotalCalories} kcal";

        /// <summary>Number of meals logged on this day, pluralised correctly.</summary>
        public string MealCountLabel =>
            Count == 1 ? "1 meal" : $"{Count} meals";

        // ── TTS Properties ───────────────────────────────────────────────

        /// <summary>
        /// Returns the list of meals for binding in XAML.
        /// </summary>
        public List<MealLogEntry> Meals => this;

        /// <summary>
        /// TTS play/pause state.
        /// </summary>
        public bool IsSpeaking
        {
            get => _isSpeaking;
            set
            {
                if (_isSpeaking != value)
                {
                    _isSpeaking = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ReadGroupButtonText));
                }
            }
        }

        /// <summary>
        /// Button label toggles between "Read" and "Stop".
        /// </summary>
        public string ReadGroupButtonText => _isSpeaking ? "Stop" : "Read";

        /// <summary>
        /// Command for reading this day's meals.
        /// </summary>
        public ICommand ReadGroupCommand { get; }

        // ── Constructor ──────────────────────────────────────────────────

        /// <summary>
        /// Builds the group from a date and its entries.
        /// </summary>
        public MealDayGroup(DateTime date, IEnumerable<MealLogEntry> entries) : base(entries)
        {
            Date = date.Date;

            // Friendly relative label for today / yesterday
            if (Date == DateTime.Today)
                DateLabel = "Today";
            else if (Date == DateTime.Today.AddDays(-1))
                DateLabel = "Yesterday";
            else
                DateLabel = date.ToString("ddd, MMM d");

            // Initialize TTS command
            ReadGroupCommand = new Command(async () => await ToggleReadGroupAsync());
        }

        // ── TTS Methods ─────────────────────────────────────────────────

        /// <summary>
        /// Toggles between reading the day's meals and stopping the speech.
        /// </summary>
        private async Task ToggleReadGroupAsync()
        {
            if (_isSpeaking)
            {
                CancelSpeaking();
                return;
            }

            await StartSpeakingAsync();
        }

        /// <summary>
        /// Starts reading the meals for this day aloud.
        /// </summary>
        private async Task StartSpeakingAsync()
        {
            if (Count == 0) return;

            // Cancel any ongoing speech
            CancelSpeaking();

            // Create new CancellationTokenSource
            _ttsCancellationTokenSource = new CancellationTokenSource();

            try
            {
                IsSpeaking = true;

                // Build the speech content
                var mealDescriptions = new List<string>();
                foreach (var meal in this)
                {
                    mealDescriptions.Add($"{meal.FoodName} at {meal.LoggedAt:h:mm tt}, {meal.Calories} calories");
                }

                var speechContent = $"On {DateLabel}, you had {MealCountLabel}: " +
                                   string.Join(". ", mealDescriptions) +
                                   $". Total calories: {TotalCaloriesLabel}.";

                await TextToSpeech.Default.SpeakAsync(
                    speechContent,
                    new SpeechOptions
                    {
                        Pitch = 1.0f,
                        Volume = 1.0f
                    },
                    cancelToken: _ttsCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // User cancelled speech, this is expected
                System.Diagnostics.Debug.WriteLine($"[MealDayGroup] Speech cancelled for {DateLabel}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MealDayGroup] Speech error: {ex.Message}");
            }
            finally
            {
                // Reset state after speech finishes
                IsSpeaking = false;
                _ttsCancellationTokenSource?.Dispose();
                _ttsCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Cancels the current speech for this group.
        /// </summary>
        private void CancelSpeaking()
        {
            if (_ttsCancellationTokenSource != null && !_ttsCancellationTokenSource.IsCancellationRequested)
            {
                _ttsCancellationTokenSource.Cancel();
            }
        }

        // ── INotifyPropertyChanged Implementation ────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}