namespace KanaanFlow.Core.Finance;

using System;

/// <summary>
/// One money movement: received, paid, or an exchange between two currencies.
/// </summary>
public sealed class Entry
{
    public string Id { get; set; } = string.Empty;

    public EntryType Type { get; set; }

    /// <summary>The amount received or paid; for an exchange, the amount given.</summary>
    public decimal Amount { get; set; }

    /// <summary>The currency received or paid; for an exchange, the currency given.</summary>
    public Currency Currency { get; set; }

    public string? ContactId { get; set; }

    public string CategoryId { get; set; } = EntryCategory.OtherId;

    public string Note { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    /// <summary>Local time of day as HH:mm, empty when recorded for another day.</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>Set when this entry moves loan money.</summary>
    public string? LoanId { get; set; }

    /// <summary>True for the entry that handed out or received the loan itself; false for repayments.</summary>
    public bool IsLoanPrincipal { get; set; }

    /// <summary>Exchange only: the amount got.</summary>
    public decimal ToAmount { get; set; }

    /// <summary>Exchange only: the currency got.</summary>
    public Currency ToCurrency { get; set; }

    /// <summary>Exchange only: 1 unit of <see cref="Currency"/> in <see cref="ToCurrency"/>.</summary>
    public decimal Rate { get; set; }

    /// <summary>Exchange only: "manual" or the name of the online source.</summary>
    public string RateSource { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public bool IsLoanRepayment => LoanId != null && !IsLoanPrincipal;

    public Entry Clone() => (Entry)MemberwiseClone();
}
