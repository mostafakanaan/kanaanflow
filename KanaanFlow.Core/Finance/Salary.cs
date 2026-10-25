namespace KanaanFlow.Core.Finance;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// An employee's monthly salary from a given month on. A later period replaces it; an amount of 0 means no salary
/// from that month (the person left).
/// </summary>
public sealed class SalaryPeriod
{
    public string ContactId { get; set; } = string.Empty;

    /// <summary>The first month this salary applies to, as yyyy-MM.</summary>
    public string FromMonth { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.USD;

    public SalaryPeriod Clone() => (SalaryPeriod)MemberwiseClone();
}

public enum SalaryState
{
    Unpaid,
    Partial,
    Paid
}

/// <summary>One month of one employee's salary: what was due and what was paid for it.</summary>
public sealed class SalaryMonthStatus
{
    public SalaryMonthStatus(string month, SalaryPeriod period, decimal paid, IReadOnlyList<Entry> payments)
    {
        Month = month;
        Due = period.Amount;
        Currency = period.Currency;
        Paid = paid;
        Payments = payments;
    }

    /// <summary>yyyy-MM.</summary>
    public string Month { get; }

    public decimal Due { get; }

    public Currency Currency { get; }

    public decimal Paid { get; }

    public IReadOnlyList<Entry> Payments { get; }

    public decimal Remaining => Math.Max(0m, Due - Paid);

    public SalaryState State => Paid >= Due ? SalaryState.Paid : Paid > 0m ? SalaryState.Partial : SalaryState.Unpaid;
}

/// <summary>Months as yyyy-MM, the way salary events store them.</summary>
public static class SalaryMonths
{
    public static string Of(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static DateOnly First(string month) => DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool IsValid(string? month) =>
        month != null && DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static string Add(string month, int months) => Of(First(month).AddMonths(months));
}

/// <summary>Works out salaries from the book: who is paid what, and which months are paid.</summary>
public static class SalaryCalculator
{
    /// <summary>Everyone with a salary history, in name order.</summary>
    public static List<Contact> Employees(Book book)
    {
        return book.Contacts
            .Where(c => book.Salaries.ContainsKey(c.Id))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The salary that applies in a month, or null when there is none (not started yet, or left).</summary>
    public static SalaryPeriod? SalaryIn(Book book, string contactId, string month)
    {
        if (!book.Salaries.TryGetValue(contactId, out List<SalaryPeriod>? periods))
        {
            return null;
        }

        SalaryPeriod? period = periods.LastOrDefault(p => string.CompareOrdinal(p.FromMonth, month) <= 0);
        return period != null && period.Amount > 0m ? period : null;
    }

    /// <summary>The salary that applies now (this month), or the next one when it starts later.</summary>
    public static SalaryPeriod? Current(Book book, string contactId, DateOnly today)
    {
        string month = SalaryMonths.Of(today);
        if (!book.Salaries.TryGetValue(contactId, out List<SalaryPeriod>? periods) || periods.Count == 0)
        {
            return null;
        }

        return periods.LastOrDefault(p => string.CompareOrdinal(p.FromMonth, month) <= 0) ?? periods[0];
    }

    public static SalaryMonthStatus? MonthOf(Book book, string contactId, string month)
    {
        SalaryPeriod? period = SalaryIn(book, contactId, month);
        if (period == null)
        {
            return null;
        }

        List<Entry> payments = book.Entries
            .Where(e => e.ContactId == contactId && e.SalaryMonth == month && e.Type == EntryType.Out && e.Currency == period.Currency)
            .ToList();
        return new SalaryMonthStatus(month, period, payments.Sum(e => e.Amount), payments);
    }

    /// <summary>Every month with a salary, from the first one up to <paramref name="today"/>'s month, newest first.</summary>
    public static List<SalaryMonthStatus> Months(Book book, string contactId, DateOnly today)
    {
        List<SalaryMonthStatus> months = new List<SalaryMonthStatus>();
        if (!book.Salaries.TryGetValue(contactId, out List<SalaryPeriod>? periods) || periods.Count == 0)
        {
            return months;
        }

        string last = SalaryMonths.Of(today);
        for (string month = periods[0].FromMonth; string.CompareOrdinal(month, last) <= 0; month = SalaryMonths.Add(month, 1))
        {
            SalaryMonthStatus? status = MonthOf(book, contactId, month);
            if (status != null)
            {
                months.Add(status);
            }
        }

        months.Reverse();
        return months;
    }

    /// <summary>Still owed per currency, over all months up to today.</summary>
    public static Dictionary<Currency, decimal> Owed(Book book, string contactId, DateOnly today)
    {
        return Months(book, contactId, today)
            .GroupBy(m => m.Currency)
            .Where(g => g.Sum(m => m.Remaining) > 0m)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Remaining));
    }
}
