using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WhatToEat.Models
{
    /// <summary>
    /// Represents a recipe returned from Spoonacular API
    /// </summary>
    public class Meal : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public int ReadyInMinutes { get; set; }
        public int Servings { get; set; }

        // Mapped properties to keep XAML bindings working
        public string StrMeal => Title;
        public string StrMealThumb => Image;
        public string StrArea => $"Ready in {ReadyInMinutes} mins";
        public string StrCategory => $"Serves {Servings}";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Wrapper for Spoonacular search response
    /// </summary>
    public class SpoonacularSearchResponse
    {
        public List<Meal> Results { get; set; } = new();
        public int TotalResults { get; set; }
    }
}