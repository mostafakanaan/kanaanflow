namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class TransactionListViewModel : BaseViewModel
{
    private readonly ITransactionRepository transactionRepository;

    private DateTime filterFrom = DateTime.Today.AddMonths(-1);
    private DateTime filterTo = DateTime.Today.AddDays(1);

    public DateTime FilterFrom
    {
        get => filterFrom;
        set => SetProperty(ref filterFrom, value);
    }

    public DateTime FilterTo
    {
        get => filterTo;
        set => SetProperty(ref filterTo, value);
    }

    public ObservableCollection<Transaction> Transactions { get; } = new ObservableCollection<Transaction>();

    public TransactionListViewModel(ITransactionRepository transactionRepositoryInstance)
    {
        transactionRepository = transactionRepositoryInstance;
        Title = "Transactions";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<Transaction> items = await transactionRepository.GetByDateRangeAsync(FilterFrom, FilterTo, CancellationToken.None);
            Transactions.Clear();
            foreach (Transaction tx in items)
            {
                Transactions.Add(tx);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AddTransactionAsync()
    {
        await Shell.Current.GoToAsync("AddTransaction");
    }

    [RelayCommand]
    public async Task DeleteAsync(Transaction transaction)
    {
        if (transaction == null)
        {
            return;
        }

        await transactionRepository.DeleteAsync(transaction.Id, CancellationToken.None);
        Transactions.Remove(transaction);
    }
}
