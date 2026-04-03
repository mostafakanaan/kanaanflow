namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.App.Localization;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Enums;
using KanaanFlow.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class AddTransactionViewModel : BaseViewModel
{
    private readonly ITransactionRepository transactionRepository;
    private readonly ICategoryRepository categoryRepository;

    private string description = string.Empty;
    private decimal amount;
    private DateTime date = DateTime.Today;
    private TransactionType type = TransactionType.Expense;
    private Currency selectedCurrency = Currency.USD;
    private Category? selectedCategory;
    private string statusMessage = string.Empty;

    public string Description
    {
        get => description;
        set => SetProperty(ref description, value);
    }

    public decimal Amount
    {
        get => amount;
        set => SetProperty(ref amount, value);
    }

    public DateTime Date
    {
        get => date;
        set => SetProperty(ref date, value);
    }

    public TransactionType Type
    {
        get => type;
        set => SetProperty(ref type, value);
    }

    public Currency SelectedCurrency
    {
        get => selectedCurrency;
        set => SetProperty(ref selectedCurrency, value);
    }

    public Category? SelectedCategory
    {
        get => selectedCategory;
        set => SetProperty(ref selectedCategory, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public ObservableCollection<Category> Categories { get; } = new ObservableCollection<Category>();
    public ObservableCollection<TransactionType> TransactionTypes { get; } = new ObservableCollection<TransactionType>
    {
        TransactionType.Income,
        TransactionType.Expense
    };
    public ObservableCollection<Currency> Currencies { get; } = new ObservableCollection<Currency>
    {
        Currency.USD,
        Currency.EUR,
        Currency.TL,
        Currency.SP
    };

    public AddTransactionViewModel(ITransactionRepository transactionRepositoryInstance, ICategoryRepository categoryRepositoryInstance)
    {
        transactionRepository = transactionRepositoryInstance;
        categoryRepository = categoryRepositoryInstance;
        Title = LocalizationManager.Instance["AddTransaction"];
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IReadOnlyList<Category> cats = await categoryRepository.GetAllAsync(CancellationToken.None);
        Categories.Clear();
        foreach (Category c in cats)
        {
            Categories.Add(c);
        }

        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (SelectedCategory == null)
        {
            StatusMessage = LocalizationManager.Instance["SelectCategory"];
            return;
        }

        if (Amount <= 0)
        {
            StatusMessage = LocalizationManager.Instance["AmountGreaterThanZero"];
            return;
        }

        Transaction tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = Amount,
            Description = Description,
            Date = Date,
            Type = Type,
            Currency = SelectedCurrency,
            CategoryId = SelectedCategory.Id,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow
        };

        await transactionRepository.AddAsync(tx, CancellationToken.None);

        await Shell.Current.GoToAsync("..");
    }
}
