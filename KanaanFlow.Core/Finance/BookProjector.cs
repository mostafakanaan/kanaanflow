namespace KanaanFlow.Core.Finance;

using System;
using System.Collections.Generic;
using System.Linq;
using KanaanFlow.Core.Finance.Events;

/// <summary>
/// Rebuilds the book by replaying its event log in time order. Replaying the same events always gives the same book,
/// so devices that hold the same events agree, whatever order the events arrived in.
/// </summary>
public static class BookProjector
{
    public static Book Project(IEnumerable<BookEvent> events)
    {
        Book book = new Book();
        HashSet<string> voidedEntries = new HashSet<string>();
        HashSet<string> voidedLoans = new HashSet<string>();
        Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        IEnumerable<BookEvent> ordered = events
            .OrderBy(e => e.AtUtc)
            .ThenBy(e => e.Id, StringComparer.Ordinal);

        foreach (BookEvent bookEvent in ordered)
        {
            Apply(book, entries, voidedEntries, voidedLoans, bookEvent.Change);
        }

        book.Entries.AddRange(entries.Values.Where(e => !voidedEntries.Contains(e.Id) && (e.LoanId == null || !voidedLoans.Contains(e.LoanId))));
        book.SortEntries();
        return book;
    }

    private static void Apply(
        Book book,
        Dictionary<string, Entry> entries,
        HashSet<string> voidedEntries,
        HashSet<string> voidedLoans,
        BookChange change)
    {
        switch (change)
        {
            case EntryRecorded recorded:
                Entry entry = recorded.Entry.Clone();
                if (book.FindEntryCategory(entry.CategoryId) == null)
                {
                    entry.CategoryId = EntryCategory.OtherId;
                }

                entries[entry.Id] = entry;

                break;

            case EntryVoided voided:
                voidedEntries.Add(voided.EntryId);
                break;

            case ContactSaved saved:
                Contact contact = saved.Contact.Clone();
                if (book.FindPeopleCategory(contact.CategoryId) == null)
                {
                    contact.CategoryId = PeopleCategory.OtherId;
                }

                Upsert(book.Contacts, contact, c => c.Id);
                break;

            case LoanOpened opened:
                Upsert(book.Loans, opened.Loan, l => l.Id);
                break;

            case LoanVoided voidedLoan:
                voidedLoans.Add(voidedLoan.LoanId);
                book.Loans.RemoveAll(l => l.Id == voidedLoan.LoanId);
                break;

            case EntryCategorySaved categorySaved:
                Upsert(book.EntryCategories, categorySaved.Category, c => c.Id);
                break;

            case EntryCategoryRemoved categoryRemoved:
                if (categoryRemoved.CategoryId != EntryCategory.OtherId && categoryRemoved.CategoryId != EntryCategory.LoanId)
                {
                    book.EntryCategories.RemoveAll(c => c.Id == categoryRemoved.CategoryId);
                    foreach (Entry moved in entries.Values.Where(e => e.CategoryId == categoryRemoved.CategoryId))
                    {
                        moved.CategoryId = EntryCategory.OtherId;
                    }
                }

                break;

            case PeopleCategorySaved peopleSaved:
                Upsert(book.PeopleCategories, peopleSaved.Category, c => c.Id);
                break;

            case PeopleCategoryRemoved peopleRemoved:
                if (peopleRemoved.CategoryId != PeopleCategory.OtherId)
                {
                    book.PeopleCategories.RemoveAll(c => c.Id == peopleRemoved.CategoryId);
                    foreach (Contact moved in book.Contacts.Where(c => c.CategoryId == peopleRemoved.CategoryId))
                    {
                        moved.CategoryId = PeopleCategory.OtherId;
                    }
                }

                break;

            case OpeningBalanceSet opening:
                book.OpeningBalances[opening.Currency] = opening.Amount;
                break;

            case MemberSaved memberSaved:
                Upsert(book.Members, memberSaved.Member, m => m.Id);
                break;

            case MemberRemoved memberRemoved:
                book.Members.RemoveAll(m => m.Id == memberRemoved.MemberId);
                break;
        }
    }

    private static void Upsert<T>(List<T> items, T item, Func<T, string> key)
    {
        string id = key(item);
        int index = items.FindIndex(x => key(x) == id);
        if (index >= 0)
        {
            items[index] = item;
        }
        else
        {
            items.Add(item);
        }
    }
}
