using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WhatToEat.ViewModels
{
    /// <summary>
    /// Global accessibility settings shared across all pages.
    /// Singleton — all pages are affected through App-level DynamicResource keys.
    ///
    /// WCAG 2.1 criteria covered:
    ///   1.4.4  Resize Text        — FontScale updates global ResourceDictionary keys
    ///   1.4.3  Contrast Minimum   — High Contrast toggle
    ///   1.4.6  Enhanced Contrast  — pure black/white palette in high-contrast mode
    ///   2.5.5  Target Size        — TouchTargetHeight grows with FontScale
    ///   4.1.2  Name/Role/Value    — SemanticProperties on all controls (in XAML)
    /// </summary>
    public class AccessibilityViewModel : INotifyPropertyChanged
    {
        // ── Singleton ─────────────────────────────────────────────────
        public static readonly AccessibilityViewModel Instance = new();
        private AccessibilityViewModel() { LoadSavedPreferences(); }

        // ── Preference keys ────────────────────────────────────────────
        private const string KeyFontScale = "a11y_font_scale";
        private const string KeyHighContrast = "a11y_high_contrast";

        // ════════════════════════════════════════════════════════════════
        // Font Scale
        // Four steps: 0.85 Small | 1.0 Normal | 1.25 Large | 1.5 XLarge
        // On change → updates App-level ResourceDictionary so every
        // Label/Button using DynamicResource FontBody etc. updates live.
        // ════════════════════════════════════════════════════════════════
        private double _fontScale = 1.0;
        public double FontScale
        {
            get => _fontScale;
            set
            {
                if (!Set(ref _fontScale, value)) return;
                Preferences.Set(KeyFontScale, value);
                ApplyFontScaleToResources();
                OnPropertyChanged(nameof(FontScaleLabel));
                // Also notify local computed props used by AccessibilityPage itself
                OnPropertyChanged(nameof(FontTiny));
                OnPropertyChanged(nameof(FontSmall));
                OnPropertyChanged(nameof(FontBody));
                OnPropertyChanged(nameof(FontMedium));
                OnPropertyChanged(nameof(FontLarge));
                OnPropertyChanged(nameof(FontTitle));
                OnPropertyChanged(nameof(FontDisplay));
                OnPropertyChanged(nameof(TouchTargetHeight));
            }
        }

        public string FontScaleLabel => FontScale switch
        {
            <= 0.85 => "Small",
            <= 1.0 => "Normal",
            <= 1.25 => "Large",
            _ => "Extra Large"
        };

        // Local computed sizes — used only by AccessibilityPage preview
        public double FontTiny => Round(10 * FontScale);
        public double FontSmall => Round(12 * FontScale);
        public double FontBody => Round(14 * FontScale);
        public double FontMedium => Round(16 * FontScale);
        public double FontLarge => Round(20 * FontScale);
        public double FontTitle => Round(24 * FontScale);
        public double FontDisplay => Round(28 * FontScale);
        public double TouchTargetHeight => FontScale switch
        {
            <= 1.0 => 44,
            <= 1.25 => 52,
            _ => 56
        };

        // ════════════════════════════════════════════════════════════════
        // High Contrast
        // ════════════════════════════════════════════════════════════════
        private bool _highContrast;
        public bool HighContrast
        {
            get => _highContrast;
            set
            {
                if (!Set(ref _highContrast, value)) return;
                Preferences.Set(KeyHighContrast, value);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Core method: writes scaled values into App.Current.Resources
        // so every DynamicResource binding in every page updates instantly
        // ════════════════════════════════════════════════════════════════
        private void ApplyFontScaleToResources()
        {
            var res = Application.Current?.Resources;
            if (res == null) return;

            res["FontTiny"] = Round(10 * _fontScale);
            res["FontSmall"] = Round(12 * _fontScale);
            res["FontBody"] = Round(14 * _fontScale);
            res["FontMedium"] = Round(16 * _fontScale);
            res["FontLarge"] = Round(20 * _fontScale);
            res["FontTitle"] = Round(24 * _fontScale);
            res["FontDisplay"] = Round(28 * _fontScale);
            res["TouchTargetHeight"] = TouchTargetHeight;
        }

        // ════════════════════════════════════════════════════════════════
        // Load saved preferences and apply on startup
        // ════════════════════════════════════════════════════════════════
        private void LoadSavedPreferences()
        {
            _fontScale = Preferences.Get(KeyFontScale, 1.0);
            _highContrast = Preferences.Get(KeyHighContrast, false);
            // Apply after a short delay so Application.Current.Resources is ready
            MainThread.BeginInvokeOnMainThread(ApplyFontScaleToResources);
        }

        // ════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════
        private static double Round(double v) => Math.Round(v, 1);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}