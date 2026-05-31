using SQLite;

namespace WhatToEat.Models
{
    /// <summary>
    /// Response from LogMeal food image classification API.
    /// </summary>
    public class FoodImageResult
    {
        public FoodCategory? Category { get; set; }
        public FoodNutrition? Nutrition { get; set; }
    }

    public class FoodCategory
    {
        public string Name { get; set; } = string.Empty;
        public double Probability { get; set; }
    }

    public class FoodNutrition
    {
        public NutrientValue? Calories { get; set; }
        public NutrientValue? Protein { get; set; }
        public NutrientValue? Fat { get; set; }
        public NutrientValue? Carbs { get; set; }
    }

    public class NutrientValue
    {
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>
    /// A logged meal entry persisted to the SQLite MealLog table.
    /// Now includes LocationName captured via Geolocation when the photo is taken.
    /// [Table]         — maps this class to the "MealLog" table.
    /// [PrimaryKey]    — Id is the unique row identifier.
    /// [AutoIncrement] — SQLite assigns the Id automatically on insert.
    /// [Ignore]        — computed display properties are not stored as columns.
    /// </summary>
    [Table("MealLog")]
    public class MealLogEntry
    {
        /// <summary>Auto-assigned unique row ID — required by sqlite-net-pcl.</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string FoodName { get; set; } = string.Empty;
        public int Calories { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public DateTime LoggedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// City / area name captured via Geolocation at time of logging.
        /// Empty string if location permission was denied or unavailable.
        /// </summary>
        public string LocationName { get; set; } = string.Empty;

        // ── Display helpers (not stored in database) ──────────────────

        /// <summary>Formatted time for display only.</summary>
        [Ignore]
        public string TimeDisplay => LoggedAt.ToString("HH:mm");

        /// <summary>Formatted date for display only.</summary>
        [Ignore]
        public string DateDisplay => LoggedAt.ToString("dd MMM yyyy");

        /// <summary>
        /// Shows "📍 Manchester" when location is available,
        /// empty string otherwise — used by HistoryPage.
        /// </summary>
        [Ignore]
        public string LocationDisplay =>
            string.IsNullOrWhiteSpace(LocationName) ? string.Empty : $"{LocationName}";

        /// <summary>True when a location was captured — controls label visibility.</summary>
        [Ignore]
        public bool HasLocation => !string.IsNullOrWhiteSpace(LocationName);
    }
}