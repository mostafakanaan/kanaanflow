namespace KanaanFlow.Core.Finance;

using System;
using System.Collections.Generic;

/// <summary>
/// A loan together with what has been repaid so far.
/// </summary>
public sealed class LoanStatus
{
    public LoanStatus(Loan loan, IReadOnlyList<LoanPayment> payments, DateOnly today)
    {
        Loan = loan;
        Payments = payments;

        decimal paid = 0m;
        foreach (LoanPayment payment in payments)
        {
            paid += payment.Amount;
        }

        Repaid = paid;
        Remaining = Math.Max(0m, loan.Amount - paid);
        IsOverdue = Remaining > 0m && loan.DueDate.HasValue && loan.DueDate.Value < today;
    }

    public Loan Loan { get; }

    public IReadOnlyList<LoanPayment> Payments { get; }

    public decimal Repaid { get; }

    public decimal Remaining { get; }

    public bool IsOpen => Remaining > 0m;

    public bool IsOverdue { get; }

    /// <summary>Share repaid, 0 to 100.</summary>
    public int PercentRepaid => Loan.Amount <= 0m ? 100 : (int)Math.Round(Math.Min(Repaid, Loan.Amount) / Loan.Amount * 100m);
}
