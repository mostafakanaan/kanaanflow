namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class LoanListPage : ContentPage
{
    private readonly LoanListViewModel viewModel;

    public LoanListPage(LoanListViewModel vm)
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
