namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;
using System;

public partial class LicensePage : ContentPage
{
    public LicensePage(LicensePageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        viewModel.LicenseValidated += OnLicenseValidated;
    }

    private async void OnLicenseValidated(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Dashboard");
    }
}
