namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class CustomersPage : ContentPage
{
    public CustomersPage(CustomersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
