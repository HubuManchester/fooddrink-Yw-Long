using WhatToEat.Models;

namespace WhatToEat.Models
{
    /// <summary>
    /// Represents a group of meal log entries for a single calendar day.
    /// Used by CollectionView's IsGrouped feature on HistoryPage.
    /// Inherits List&lt;MealLogEntry&gt; so MAUI's grouping engine can iterate items directly.
    /// </summary>
    public class MealDayGroup : List<MealLogEntry>
    {
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
        }
    }
}