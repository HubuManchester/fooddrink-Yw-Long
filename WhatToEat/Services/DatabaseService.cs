using SQLite;
using WhatToEat.Models;

namespace WhatToEat.Services
{
    /// <summary>
    /// SQLite-backed persistent storage.
    /// Manages two tables:
    ///   • MealLog    — photos taken on PicturePage
    ///   • Favourites — recipes saved from DiscoverPage
    ///
    /// Singleton pattern: use DatabaseService.Instance throughout the app.
    /// All methods are async to avoid blocking the UI thread.
    /// </summary>
    public class DatabaseService
    {
        // ── Singleton ─────────────────────────────────────────────────────

        public static readonly DatabaseService Instance = new();
        private DatabaseService() { }

        // ── Connection ────────────────────────────────────────────────────

        private SQLiteAsyncConnection? _db;

        /// <summary>
        /// Opens (or creates) the database and creates both tables if missing.
        /// Called lazily on first use — no need to call manually.
        /// </summary>
        private async Task InitAsync()
        {
            if (_db != null) return;

            var dbPath = Path.Combine(
                FileSystem.AppDataDirectory, "whatttoeat.db3");

            _db = new SQLiteAsyncConnection(dbPath,
                SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create |
                SQLiteOpenFlags.SharedCache);

            // Create both tables if they don't exist yet
            await _db.CreateTableAsync<MealLogEntry>();
            await _db.CreateTableAsync<FavouriteMeal>();
        }

        // ══════════════════════════════════════════════════════════════════
        // MEAL LOG — PicturePage
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Returns all logged meals, newest first.</summary>
        public async Task<List<MealLogEntry>> GetAllMealsAsync()
        {
            await InitAsync();
            return await _db!.Table<MealLogEntry>()
                             .OrderByDescending(e => e.LoggedAt)
                             .ToListAsync();
        }

        /// <summary>Inserts a new meal and stamps its auto-increment Id.</summary>
        public async Task AddMealAsync(MealLogEntry entry)
        {
            await InitAsync();
            await _db!.InsertAsync(entry);
        }

        /// <summary>Deletes a single meal entry.</summary>
        public async Task DeleteMealAsync(MealLogEntry entry)
        {
            await InitAsync();
            await _db!.DeleteAsync(entry);
        }

        /// <summary>Wipes the entire meal log.</summary>
        public async Task ClearAllMealsAsync()
        {
            await InitAsync();
            await _db!.ExecuteAsync("DELETE FROM MealLog");
        }

        // ══════════════════════════════════════════════════════════════════
        // FAVOURITES — DiscoverPage
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Returns all saved favourites, newest first.</summary>
        public async Task<List<FavouriteMeal>> GetAllFavouritesAsync()
        {
            await InitAsync();
            return await _db!.Table<FavouriteMeal>()
                             .OrderByDescending(f => f.SavedAt)
                             .ToListAsync();
        }

        /// <summary>Saves a recipe to favourites.</summary>
        public async Task AddFavouriteAsync(FavouriteMeal favourite)
        {
            await InitAsync();
            await _db!.InsertAsync(favourite);
        }

        /// <summary>Removes a single favourite.</summary>
        public async Task DeleteFavouriteAsync(FavouriteMeal favourite)
        {
            await InitAsync();
            await _db!.DeleteAsync(favourite);
        }

        /// <summary>
        /// Checks if a Spoonacular recipe is already in favourites.
        /// Used by DiscoverPage to show a filled/outlined heart icon.
        /// </summary>
        public async Task<bool> IsFavouriteAsync(int spoonacularId)
        {
            await InitAsync();
            var count = await _db!.Table<FavouriteMeal>()
                                  .Where(f => f.SpoonacularId == spoonacularId)
                                  .CountAsync();
            return count > 0;
        }

        /// <summary>
        /// Toggles favourite status for a Meal.
        /// Returns true if the meal was added, false if it was removed.
        /// </summary>
        public async Task<bool> ToggleFavouriteAsync(Meal meal)
        {
            await InitAsync();

            var existing = await _db!.Table<FavouriteMeal>()
                                     .Where(f => f.SpoonacularId == meal.Id)
                                     .FirstOrDefaultAsync();

            if (existing != null)
            {
                // Already saved — remove it
                await _db.DeleteAsync(existing);
                return false;
            }
            else
            {
                // Not saved — add it
                await _db.InsertAsync(new FavouriteMeal
                {
                    SpoonacularId = meal.Id,
                    Title = meal.Title,
                    ImageUrl = meal.Image,
                    ReadyInMinutes = meal.ReadyInMinutes,
                    Servings = meal.Servings,
                    SavedAt = DateTime.Now
                });
                return true;
            }
        }
    }
}