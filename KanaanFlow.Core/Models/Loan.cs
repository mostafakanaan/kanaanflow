namespace KanaanFlow.Core.Models;

using KanaanFlow.Core.Enums;
using System;

public sealed class Loan
{
    public Guid Id { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal RemainingAmount { get; set; }
    public PaymentDirection Direction { get; set; }
    public LoanStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedUtc { get; set; }
}
