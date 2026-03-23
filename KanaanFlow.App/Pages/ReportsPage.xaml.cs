namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class ReportsPage : ContentPage
{
    private readonly ReportsViewModel viewModel;

    public ReportsPage(ReportsViewModel vm)
    {
        InitializeComponent();
        viewModel = vm;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.LoadCommand.ExecuteAsync(null);
    }
}
