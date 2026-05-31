using WhatToEat.ViewModels;

namespace WhatToEat.Views;

public partial class DiscoverPage : ContentPage
{
    public DiscoverPage()
    {
        InitializeComponent();
        BindingContext = new DiscoverViewModel();
    }

    private async void OnLogMealClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Picture");
    }
}