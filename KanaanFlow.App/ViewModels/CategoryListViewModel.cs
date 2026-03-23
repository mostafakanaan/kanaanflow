namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class CategoryListViewModel : BaseViewModel
{
    private readonly ICategoryRepository categoryRepository;

    private string newCategoryName = string.Empty;
    private string newCategoryIcon = string.Empty;
    private string newCategoryColor = "#808080";
    private string statusMessage = string.Empty;

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    public string NewCategoryIcon
    {
        get => newCategoryIcon;
        set => SetProperty(ref newCategoryIcon, value);
    }

    public string NewCategoryColor
    {
        get => newCategoryColor;
        set => SetProperty(ref newCategoryColor, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public ObservableCollection<Category> Categories { get; } = new ObservableCollection<Category>();

    public CategoryListViewModel(ICategoryRepository categoryRepositoryInstance)
    {
        categoryRepository = categoryRepositoryInstance;
        Title = "Categories";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<Category> items = await categoryRepository.GetAllAsync(CancellationToken.None);
            Categories.Clear();
            foreach (Category c in items)
            {
                Categories.Add(c);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusMessage = "Category name is required.";
            return;
        }

        Category category = new Category
        {
            Id = Guid.NewGuid(),
            Name = NewCategoryName,
            Icon = NewCategoryIcon,
            Color = NewCategoryColor
        };

        await categoryRepository.AddAsync(category, CancellationToken.None);
        Categories.Add(category);
        StatusMessage = "Category added!";
        NewCategoryName = string.Empty;
        NewCategoryIcon = string.Empty;
    }

    [RelayCommand]
    public async Task DeleteAsync(Category category)
    {
        if (category == null)
        {
            return;
        }

        await categoryRepository.DeleteAsync(category.Id, CancellationToken.None);
        Categories.Remove(category);
    }
}
