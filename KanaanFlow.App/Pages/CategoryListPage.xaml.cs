namespace KanaanFlow.App.Pages;

using KanaanFlow.App.ViewModels;

public partial class CategoryListPage : ContentPage
{
    private readonly CategoryListViewModel viewModel;

    public CategoryListPage(CategoryListViewModel vm)
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
