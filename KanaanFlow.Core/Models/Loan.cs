namespace KanaanFlow.Core.Models;

using KanaanFlow.Core.Enums;
using KanaanFlow.Core.Helpers;
using System;

public sealed class Loan
{
    public Guid Id { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal RemainingAmount { get; set; }
    public PaymentDirection Direction { get; set; }
    public LoanStatus Status { get; set; }
    public Currency Currency { get; set; } = Currency.USD;
    public DateTime? DueDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }

    public string FormattedAmount => CurrencyHelper.Format(Amount, Currency);
    public string FormattedRemainingAmount => CurrencyHelper.Format(RemainingAmount, Currency);
}
