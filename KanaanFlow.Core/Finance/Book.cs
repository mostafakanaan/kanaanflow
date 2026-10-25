namespace KanaanFlow.Core.Finance;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The current state of the money book, rebuilt from its event log by <see cref="BookProjector"/>.
/// </summary>
public sealed class Book
{
    public static readonly Currency[] Currencies = { Currency.SYP, Currency.USD, Currency.EUR, Currency.TRY };

    public List<Contact> Contacts { get; } = new List<Contact>();

    /// <summary>Entries that have not been undone, newest first.</summary>
    public List<Entry> Entries { get; } = new List<Entry>();

    public List<Loan> Loans { get; } = new List<Loan>();

    public List<PeopleCategory> PeopleCategories { get; } = DefaultCategories.People();

    public List<EntryCategory> EntryCategories { get; } = DefaultCategories.Entries();

    /// <summary>Everyone who has been on the team, including removed members (see <see cref="TeamMember.Active"/>).</summary>
    public List<TeamMember> Members { get; } = new List<TeamMember>();

    public IEnumerable<TeamMember> ActiveMembers => Members.Where(m => m.Active);

    public Dictionary<Currency, decimal> OpeningBalances { get; } = Currencies.ToDictionary(c => c, c => 0m);

    public Contact? FindContact(string? id) => id == null ? null : Contacts.FirstOrDefault(c => c.Id == id);

    public Loan? FindLoan(string? id) => id == null ? null : Loans.FirstOrDefault(l => l.Id == id);

    public TeamMember? FindMember(string? id) => id == null ? null : Members.FirstOrDefault(m => m.Id == id);

    public EntryCategory? FindEntryCategory(string? id) => id == null ? null : EntryCategories.FirstOrDefault(c => c.Id == id);

    public PeopleCategory? FindPeopleCategory(string? id) => id == null ? null : PeopleCategories.FirstOrDefault(c => c.Id == id);

    /// <summary>Puts entries in display order: newest day first, then newest recorded first.</summary>
    public void SortEntries()
    {
        Entries.Sort((a, b) =>
        {
            int byDate = b.Date.CompareTo(a.Date);
            if (byDate != 0)
            {
                return byDate;
            }

            int byCreated = b.CreatedUtc.CompareTo(a.CreatedUtc);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(b.Id, a.Id);
        });
    }
}
