using WhatToEat.ViewModels;

namespace WhatToEat.Views;

/// <summary>
/// Code-behind for AccessibilityPage.
/// All settings are persisted via AccessibilityViewModel (Preferences API).
/// </summary>
public partial class AccessibilityPage : ContentPage
{
    private readonly AccessibilityViewModel _vm = AccessibilityViewModel.Instance;

    public AccessibilityPage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    // ── Font size quick-select buttons ────────────────────────────────
    private void OnFontSmallClicked(object sender, EventArgs e)   => _vm.FontScale = 0.85;
    private void OnFontNormalClicked(object sender, EventArgs e)  => _vm.FontScale = 1.0;
    private void OnFontLargeClicked(object sender, EventArgs e)   => _vm.FontScale = 1.25;
    private void OnFontXLargeClicked(object sender, EventArgs e)  => _vm.FontScale = 1.5;

    // ── System settings shortcuts ─────────────────────────────────────

    /// <summary>
    /// Opens the device Display settings so the user can toggle dark mode.
    /// Falls back silently if the OS does not support deep-linking.
    /// </summary>
    private async void OnOpenDisplaySettingsClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var intent = new Android.Content.Intent(
                Android.Provider.Settings.ActionDisplaySettings);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
#else
            await Shell.Current.DisplayAlert(
                "Display Settings",
                "Please open your device Settings → Display to change the theme.",
                "OK");
#endif
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Display Settings",
                "Please open your device Settings → Display to change the theme.",
                "OK");
        }
    }

    /// <summary>
    /// Opens the device Accessibility settings for TalkBack / VoiceOver.
    /// </summary>
    private async void OnOpenAccessibilitySettingsClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var intent = new Android.Content.Intent(
                Android.Provider.Settings.ActionAccessibilitySettings);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
#else
            await Shell.Current.DisplayAlert(
                "Accessibility Settings",
                "Please open your device Settings → Accessibility to enable TalkBack or VoiceOver.",
                "OK");
#endif
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Accessibility Settings",
                "Please open your device Settings → Accessibility to enable screen readers.",
                "OK");
        }
    }
}