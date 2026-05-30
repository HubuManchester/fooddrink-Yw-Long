using WhatToEat.ViewModels;

namespace WhatToEat.Views;

public partial class PicturePage : ContentPage
{
    public PicturePage()
    {
        InitializeComponent();
        BindingContext = new PictureViewModel();
    }
}