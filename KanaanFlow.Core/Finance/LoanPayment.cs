namespace KanaanFlow.Core.Finance;

using System;

public sealed class LoanPayment
{
    public string EntryId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }
}
