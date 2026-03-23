namespace KanaanFlow.Core.Models;

using KanaanFlow.Core.Enums;
using System;

public sealed class Transaction
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TransactionType Type { get; set; }
    public Guid CategoryId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }

    public Category? Category { get; set; }
}
