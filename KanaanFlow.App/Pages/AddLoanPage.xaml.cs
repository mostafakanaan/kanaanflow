namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class AddLoanPage : ContentPage
{
    private readonly AddLoanViewModel viewModel;

    public AddLoanPage(AddLoanViewModel vm)
    {
        InitializeComponent();
        viewModel = vm;
        BindingContext = viewModel;
    }
}
