namespace KanaanFlow.Core.Models;

using System;

public sealed class DailyReport
{
    public DateTime Date { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Balance => TotalIncome - TotalExpense;
    public int TransactionCount { get; set; }
}
