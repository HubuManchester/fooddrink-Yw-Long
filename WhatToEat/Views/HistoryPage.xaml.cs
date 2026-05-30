using WhatToEat.ViewModels;

namespace WhatToEat.Views;

/// <summary>
/// Code-behind for History page.
/// Reload meal history automatically every time the page appears,
/// to display newly added records from Picture page.
/// </summary>
public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage()
    {
        InitializeComponent();
        _viewModel = new HistoryViewModel();
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Triggered when page becomes visible to user.
    /// Refresh data to synchronize latest meal records.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshCommand.Execute(null);
    }
}