namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class ReportsPage : ContentPage
{
    public ReportsPage(ReportsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
