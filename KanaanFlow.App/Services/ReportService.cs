namespace KanaanFlow.App.Services;

using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Enums;
using KanaanFlow.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class ReportService : IReportService
{
    private readonly ITransactionRepository transactionRepository;

    public ReportService(ITransactionRepository transactionRepositoryInstance)
    {
        transactionRepository = transactionRepositoryInstance;
    }

    public async Task<DailyReport> GenerateDailySummaryAsync(DateTime date, CancellationToken cancellationToken)
    {
        DateTime from = date.Date;
        DateTime to = from.AddDays(1);

        IReadOnlyList<Transaction> transactions = await transactionRepository.GetByDateRangeAsync(from, to, cancellationToken);

        decimal totalIncome = transactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        decimal totalExpense = transactions.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);

        return new DailyReport
        {
            Date = date.Date,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            TransactionCount = transactions.Count
        };
    }

    public async Task<IReadOnlyList<DailyReport>> GenerateWeeklySummaryAsync(DateTime weekStart, CancellationToken cancellationToken)
    {
        DateTime from = weekStart.Date;
        DateTime to = from.AddDays(7);

        IReadOnlyList<Transaction> transactions = await transactionRepository.GetByDateRangeAsync(from, to, cancellationToken);

        List<DailyReport> reports = new List<DailyReport>();
        for (int i = 0; i < 7; i++)
        {
            DateTime day = from.AddDays(i).Date;
            IEnumerable<Transaction> dayTx = transactions.Where(x => x.Date >= day && x.Date < day.AddDays(1));

            reports.Add(new DailyReport
            {
                Date = day,
                TotalIncome = dayTx.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
                TotalExpense = dayTx.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount),
                TransactionCount = dayTx.Count()
            });
        }
        return reports;
    }

    public async Task<IReadOnlyList<DailyReport>> GenerateMonthlySummaryAsync(int year, int month, CancellationToken cancellationToken)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime from = new DateTime(year, month, 1);
        DateTime to = from.AddMonths(1);

        IReadOnlyList<Transaction> transactions = await transactionRepository.GetByDateRangeAsync(from, to, cancellationToken);

        List<DailyReport> reports = new List<DailyReport>();
        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime date = new DateTime(year, month, day);
            IEnumerable<Transaction> dayTx = transactions.Where(x => x.Date >= date && x.Date < date.AddDays(1));

            reports.Add(new DailyReport
            {
                Date = date,
                TotalIncome = dayTx.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
                TotalExpense = dayTx.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount),
                TransactionCount = dayTx.Count()
            });
        }
        return reports;
    }
}
