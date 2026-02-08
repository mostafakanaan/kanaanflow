namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class FamilyPage : ContentPage
{
    public FamilyPage(FamilyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
