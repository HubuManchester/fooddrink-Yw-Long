using WhatToEat.ViewModels;

namespace WhatToEat.Views;

public partial class DiscoverPage : ContentPage
{
    public DiscoverPage()
    {
        InitializeComponent();
        BindingContext = new DiscoverViewModel();
    }

    /// <summary>
    /// Navigates to PicturePage to log the meal with camera.
    /// </summary>
    private async void OnLogMealClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Picture");
    }
}