namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Enums;
using KanaanFlow.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class LoanListViewModel : BaseViewModel
{
    private readonly ILoanRepository loanRepository;

    public ObservableCollection<Loan> ActiveLoans { get; } = new ObservableCollection<Loan>();
    public ObservableCollection<Loan> PaidOffLoans { get; } = new ObservableCollection<Loan>();
    public ObservableCollection<Loan> OverdueLoans { get; } = new ObservableCollection<Loan>();

    public LoanListViewModel(ILoanRepository loanRepositoryInstance)
    {
        loanRepository = loanRepositoryInstance;
        Title = "Loans";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<Loan> all = await loanRepository.GetAllAsync(CancellationToken.None);

            ActiveLoans.Clear();
            PaidOffLoans.Clear();
            OverdueLoans.Clear();

            foreach (Loan loan in all)
            {
                if (loan.Status == LoanStatus.Active)
                {
                    ActiveLoans.Add(loan);
                }
                else if (loan.Status == LoanStatus.PaidOff)
                {
                    PaidOffLoans.Add(loan);
                }
                else if (loan.Status == LoanStatus.Overdue)
                {
                    OverdueLoans.Add(loan);
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
