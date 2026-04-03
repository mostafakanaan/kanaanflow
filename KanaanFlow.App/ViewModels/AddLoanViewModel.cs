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

public sealed partial class AddLoanViewModel : BaseViewModel
{
    private readonly ILoanRepository loanRepository;

    private string contactName = string.Empty;
    private decimal amount;
    private PaymentDirection direction = PaymentDirection.Given;
    private Currency selectedCurrency = Currency.USD;
    private bool hasDueDate;
    private DateTime dueDateValue = DateTime.Today;
    private string statusMessage = string.Empty;

    public string ContactName
    {
        get => contactName;
        set => SetProperty(ref contactName, value);
    }

    public decimal Amount
    {
        get => amount;
        set => SetProperty(ref amount, value);
    }

    public PaymentDirection Direction
    {
        get => direction;
        set => SetProperty(ref direction, value);
    }

    public Currency SelectedCurrency
    {
        get => selectedCurrency;
        set => SetProperty(ref selectedCurrency, value);
    }

    public bool HasDueDate
    {
        get => hasDueDate;
        set => SetProperty(ref hasDueDate, value);
    }

    public DateTime DueDateValue
    {
        get => dueDateValue;
        set => SetProperty(ref dueDateValue, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public ObservableCollection<PaymentDirection> Directions { get; } = new ObservableCollection<PaymentDirection>
    {
        PaymentDirection.Given,
        PaymentDirection.Received
    };

    public ObservableCollection<Currency> Currencies { get; } = new ObservableCollection<Currency>
    {
        Currency.USD,
        Currency.EUR,
        Currency.TL,
        Currency.SP
    };

    public AddLoanViewModel(ILoanRepository loanRepositoryInstance)
    {
        loanRepository = loanRepositoryInstance;
        Title = LocalizationManager.Instance["AddLoan"];
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ContactName))
        {
            StatusMessage = LocalizationManager.Instance["ContactRequired"];
            return;
        }

        if (Amount <= 0)
        {
            StatusMessage = LocalizationManager.Instance["AmountGreaterThanZero"];
            return;
        }

        Loan loan = new Loan
        {
            Id = Guid.NewGuid(),
            ContactName = ContactName,
            Amount = Amount,
            RemainingAmount = Amount,
            Direction = Direction,
            Currency = SelectedCurrency,
            Status = LoanStatus.Active,
            DueDate = HasDueDate ? DueDateValue : null,
            CreatedUtc = DateTime.UtcNow
        };

        await loanRepository.AddAsync(loan, CancellationToken.None);

        await Shell.Current.GoToAsync("..");
    }
}
