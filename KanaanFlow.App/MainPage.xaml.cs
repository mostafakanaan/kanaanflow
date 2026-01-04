namespace KanaanFlow.App;

using KanaanFlow.App.ViewModels;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MainPageViewModel vm)
        {
            await vm.RefreshAsync();
        }
    }
}
