using System.Globalization;

namespace WhatToEat.Converters
{
    /// <summary>
    /// Converts a file-system path string (e.g. from MealLogEntry.ImagePath)
    /// into an <see cref="ImageSource"/> that MAUI's Image control can display.
    ///
    /// Registration in App.xaml or MauiProgram.cs (as a static resource):
    ///   &lt;converters:PathToImageSourceConverter x:Key="PathToImageSourceConverter"/&gt;
    ///
    /// Usage in XAML:
    ///   Source="{Binding ImagePath, Converter={StaticResource PathToImageSourceConverter}}"
    /// </summary>
    public class PathToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Return null (no image) if the path is empty or null
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            // If the file exists on disk, load it; otherwise return null
            return File.Exists(path)
                ? ImageSource.FromFile(path)
                : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("PathToImageSourceConverter is one-way only.");
    }
}