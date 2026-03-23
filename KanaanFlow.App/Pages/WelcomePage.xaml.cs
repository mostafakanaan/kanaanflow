namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class WelcomePage : ContentPage
{
    private readonly WelcomePageViewModel viewModel;

    public WelcomePage(WelcomePageViewModel viewModelInstance)
    {
        InitializeComponent();
        viewModel = viewModelInstance;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await viewModel.LoadAsync();

        if (viewModel.ShouldRedirectToLicense)
        {
            await Shell.Current.GoToAsync("//License");
        }
    }
}
