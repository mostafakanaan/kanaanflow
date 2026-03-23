namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class DashboardViewModel : BaseViewModel
{
    private readonly ITransactionRepository transactionRepository;

    private decimal todayIncome;
    private decimal todayExpense;
    private decimal balance;

    public decimal TodayIncome
    {
        get => todayIncome;
        set => SetProperty(ref todayIncome, value);
    }

    public decimal TodayExpense
    {
        get => todayExpense;
        set => SetProperty(ref todayExpense, value);
    }

    public decimal Balance
    {
        get => balance;
        set => SetProperty(ref balance, value);
    }

    public ObservableCollection<Transaction> RecentTransactions { get; } = new ObservableCollection<Transaction>();

    public DashboardViewModel(ITransactionRepository transactionRepositoryInstance)
    {
        transactionRepository = transactionRepositoryInstance;
        Title = "Dashboard";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            IReadOnlyList<Transaction> todayTx = await transactionRepository.GetByDateRangeAsync(today, tomorrow, CancellationToken.None);
            IReadOnlyList<Transaction> allTx = await transactionRepository.GetAllAsync(CancellationToken.None);

            TodayIncome = todayTx
                .Where(x => x.Type == Core.Enums.TransactionType.Income)
                .Sum(x => x.Amount);

            TodayExpense = todayTx
                .Where(x => x.Type == Core.Enums.TransactionType.Expense)
                .Sum(x => x.Amount);

            Balance = TodayIncome - TodayExpense;

            RecentTransactions.Clear();
            foreach (Transaction tx in allTx.Take(5))
            {
                RecentTransactions.Add(tx);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
