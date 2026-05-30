using WhatToEat.Models;

namespace WhatToEat.Services
{
    /// <summary>
    /// Simple in-memory log of meals captured on PicturePage.
    /// Static so HistoryPage can access the same list.
    /// </summary>
    public static class MealLogService
    {
        public static List<MealLogEntry> Entries { get; } = new();

        public static void Add(MealLogEntry entry)
        {
            Entries.Insert(0, entry); // newest first
        }
    }
}