namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class TransactionListPage : ContentPage
{
    private readonly TransactionListViewModel viewModel;

    public TransactionListPage(TransactionListViewModel vm)
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
