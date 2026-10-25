namespace KanaanFlow.Core.Finance;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Balances, totals, loan status and reports, all worked out from a <see cref="Book"/>.
/// </summary>
public static class BookCalculator
{
    /// <summary>Cash on hand per currency: opening balance plus money in, minus money out, adjusted by exchanges.</summary>
    public static Dictionary<Currency, decimal> CashBalances(Book book)
    {
        Dictionary<Currency, decimal> balances = new Dictionary<Currency, decimal>(book.OpeningBalances);
        foreach (Currency currency in Book.Currencies)
        {
            balances.TryAdd(currency, 0m);
        }

        foreach (Entry entry in book.Entries)
        {
            switch (entry.Type)
            {
                case EntryType.In:
                    balances[entry.Currency] += entry.Amount;
                    break;
                case EntryType.Out:
                    balances[entry.Currency] -= entry.Amount;
                    break;
                case EntryType.Exchange:
                    balances[entry.Currency] -= entry.Amount;
                    balances[entry.ToCurrency] += entry.ToAmount;
                    break;
            }
        }

        return balances;
    }

    /// <summary>How much each currency changed on one day, exchanges included.</summary>
    public static Dictionary<Currency, decimal> DayChange(Book book, DateOnly day)
    {
        Dictionary<Currency, decimal> change = Book.Currencies.ToDictionary(c => c, c => 0m);
        foreach (Entry entry in book.Entries.Where(e => e.Date == day))
        {
            switch (entry.Type)
            {
                case EntryType.In:
                    change[entry.Currency] += entry.Amount;
                    break;
                case EntryType.Out:
                    change[entry.Currency] -= entry.Amount;
                    break;
                case EntryType.Exchange:
                    change[entry.Currency] -= entry.Amount;
                    change[entry.ToCurrency] += entry.ToAmount;
                    break;
            }
        }

        return change;
    }

    public static LoanStatus LoanStatusOf(Book book, Loan loan, DateOnly today)
    {
        List<LoanPayment> payments = book.Entries
            .Where(e => e.LoanId == loan.Id && e.IsLoanRepayment)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedUtc)
            .Select(e => new LoanPayment { EntryId = e.Id, Amount = e.Amount, Date = e.Date })
            .ToList();

        return new LoanStatus(loan, payments, today);
    }

    public static List<LoanStatus> LoanStatuses(Book book, DateOnly today)
    {
        return book.Loans.Select(l => LoanStatusOf(book, l, today)).ToList();
    }

    /// <summary>Remaining amount per currency for all loans in one direction.</summary>
    public static Dictionary<Currency, decimal> LoanTotals(Book book, LoanDirection direction, DateOnly today)
    {
        Dictionary<Currency, decimal> totals = new Dictionary<Currency, decimal>();
        foreach (LoanStatus status in LoanStatuses(book, today).Where(s => s.Loan.Direction == direction && s.IsOpen))
        {
            totals.TryGetValue(status.Loan.Currency, out decimal current);
            totals[status.Loan.Currency] = current + status.Remaining;
        }

        return totals;
    }

    /// <summary>Received from and paid to one contact, per currency that has activity.</summary>
    public static List<CurrencyTotals> ContactTotals(Book book, string contactId)
    {
        return Totals(book.Entries.Where(e => e.ContactId == contactId && e.Type != EntryType.Exchange));
    }

    public static ReportResult Report(Book book, ReportFilter filter)
    {
        Dictionary<string, string> contactCategories = book.Contacts.ToDictionary(c => c.Id, c => c.CategoryId);

        List<Entry> matching = book.Entries.Where(e =>
        {
            if (e.Type == EntryType.Exchange || e.Date < filter.From || e.Date > filter.To)
            {
                return false;
            }

            if (filter.EntryCategoryIds.Count > 0 && !filter.EntryCategoryIds.Contains(e.CategoryId))
            {
                return false;
            }

            if (filter.PeopleCategoryIds.Count > 0)
            {
                if (e.ContactId == null
                    || !contactCategories.TryGetValue(e.ContactId, out string? category)
                    || !filter.PeopleCategoryIds.Contains(category))
                {
                    return false;
                }
            }

            return filter.ContactId == null || e.ContactId == filter.ContactId;
        }).ToList();

        return new ReportResult(filter, matching, Totals(matching));
    }

    private static List<CurrencyTotals> Totals(IEnumerable<Entry> entries)
    {
        List<Entry> list = entries.ToList();
        return Book.Currencies
            .Select(c => new CurrencyTotals(
                c,
                list.Where(e => e.Currency == c && e.Type == EntryType.In).Sum(e => e.Amount),
                list.Where(e => e.Currency == c && e.Type == EntryType.Out).Sum(e => e.Amount)))
            .Where(t => t.HasActivity)
            .ToList();
    }
}
