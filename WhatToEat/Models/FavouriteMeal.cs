using SQLite;

namespace WhatToEat.Models
{
    /// <summary>
    /// A recipe saved to the user's favourites.
    /// Persisted in the SQLite "Favourites" table via DatabaseService.
    /// </summary>
    [Table("Favourites")]
    public class FavouriteMeal
    {
        /// <summary>Auto-assigned unique row ID.</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Spoonacular recipe ID — used to fetch full details later.</summary>
        public int SpoonacularId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int ReadyInMinutes { get; set; }
        public int Servings { get; set; }

        /// <summary>When the user saved this recipe.</summary>
        public DateTime SavedAt { get; set; } = DateTime.Now;

        // ── Display helpers (not stored) ─────────────────────────────────

        [Ignore]
        public string ReadyLabel => $"Ready in {ReadyInMinutes} mins";

        [Ignore]
        public string ServingsLabel => $"Serves {Servings}";

        [Ignore]
        public string SavedDateLabel => SavedAt.ToString("dd MMM yyyy");
    }
}