namespace KanaanFlow.Core.Finance;

using System;

/// <summary>
/// A simple loan: no interest, a due date, and repayments recorded as entries.
/// </summary>
public sealed class Loan
{
    public string Id { get; set; } = string.Empty;

    public LoanDirection Direction { get; set; }

    public string ContactId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Currency Currency { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}
