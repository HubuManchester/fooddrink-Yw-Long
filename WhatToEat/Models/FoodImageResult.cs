using SQLite;

namespace WhatToEat.Models
{
    /// <summary>
    /// Response from Spoonacular POST /food/images/classify
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

        /// <summary>Formatted time for display only — not stored in the database.</summary>
        [Ignore]
        public string TimeDisplay => LoggedAt.ToString("HH:mm");

        /// <summary>Formatted date for display only — not stored in the database.</summary>
        [Ignore]
        public string DateDisplay => LoggedAt.ToString("dd MMM yyyy");
    }
}