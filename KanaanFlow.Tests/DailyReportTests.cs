namespace KanaanFlow.Tests;

using KanaanFlow.Core.Models;
using System;
using Xunit;

public class DailyReportTests
{
    [Fact]
    public void DailyReport_Balance_IsIncomeMinusExpense()
    {
        DailyReport report = new DailyReport
        {
            Date = DateTime.Today,
            TotalIncome = 1000m,
            TotalExpense = 350m,
            TransactionCount = 5
        };

        Assert.Equal(650m, report.Balance);
    }

    [Fact]
    public void DailyReport_WithZeroValues_BalanceIsZero()
    {
        DailyReport report = new DailyReport
        {
            Date = DateTime.Today,
            TotalIncome = 0m,
            TotalExpense = 0m,
            TransactionCount = 0
        };

        Assert.Equal(0m, report.Balance);
    }

    [Fact]
    public void DailyReport_NegativeBalance_WhenExpenseExceedsIncome()
    {
        DailyReport report = new DailyReport
        {
            Date = DateTime.Today,
            TotalIncome = 200m,
            TotalExpense = 500m,
            TransactionCount = 3
        };

        Assert.Equal(-300m, report.Balance);
        Assert.True(report.Balance < 0);
    }

    [Fact]
    public void DailyReport_TransactionCount_StoresCorrectly()
    {
        DailyReport report = new DailyReport
        {
            TransactionCount = 12
        };

        Assert.Equal(12, report.TransactionCount);
    }
}
