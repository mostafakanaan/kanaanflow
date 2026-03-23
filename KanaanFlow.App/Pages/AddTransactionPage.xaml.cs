namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class AddTransactionPage : ContentPage
{
    private readonly AddTransactionViewModel viewModel;

    public AddTransactionPage(AddTransactionViewModel vm)
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
