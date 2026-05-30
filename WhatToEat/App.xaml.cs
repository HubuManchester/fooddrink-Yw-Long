using WhatToEat.ViewModels;

namespace WhatToEat
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Initialise accessibility singleton early so saved font scale
            // is applied to App.Current.Resources before any page renders.
            // The Instance getter triggers the private constructor which
            // calls LoadSavedPreferences → ApplyFontScaleToResources.
            _ = AccessibilityViewModel.Instance;

            MainPage = new AppShell();
        }
    }
}