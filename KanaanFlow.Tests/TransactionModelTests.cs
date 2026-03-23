namespace KanaanFlow.Tests;

using KanaanFlow.Core.Enums;
using KanaanFlow.Core.Models;
using System;
using Xunit;

public class TransactionModelTests
{
    [Fact]
    public void Transaction_DefaultValues_AreCorrect()
    {
        Transaction tx = new Transaction();

        Assert.Equal(Guid.Empty, tx.Id);
        Assert.Equal(0m, tx.Amount);
        Assert.Equal(string.Empty, tx.Description);
        Assert.Equal(default(DateTime), tx.Date);
        Assert.Equal(TransactionType.Income, tx.Type);
        Assert.Equal(Guid.Empty, tx.CategoryId);
    }

    [Fact]
    public void Transaction_WithValues_StoresCorrectly()
    {
        Guid id = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        Transaction tx = new Transaction
        {
            Id = id,
            Amount = 150.75m,
            Description = "Grocery shopping",
            Date = now,
            Type = TransactionType.Expense,
            CreatedUtc = now,
            ModifiedUtc = now
        };

        Assert.Equal(id, tx.Id);
        Assert.Equal(150.75m, tx.Amount);
        Assert.Equal("Grocery shopping", tx.Description);
        Assert.Equal(TransactionType.Expense, tx.Type);
    }

    [Fact]
    public void Transaction_IncomeType_IsDistinctFromExpense()
    {
        Transaction income = new Transaction { Type = TransactionType.Income };
        Transaction expense = new Transaction { Type = TransactionType.Expense };

        Assert.NotEqual(income.Type, expense.Type);
    }
}
