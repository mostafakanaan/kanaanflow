namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class PartnersPage : ContentPage
{
    public PartnersPage(PartnersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
