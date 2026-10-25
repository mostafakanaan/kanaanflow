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
        Dictionary<string, BookEvent> approvals = new Dictionary<string, BookEvent>();

        // The latest delete or restore per entry, and entries deleted for good.
        Dictionary<string, BookEvent> deletions = new Dictionary<string, BookEvent>();
        HashSet<string> purged = new HashSet<string>();

        IEnumerable<BookEvent> ordered = events
            .OrderBy(e => e.AtUtc)
            .ThenBy(e => e.Id, StringComparer.Ordinal);

        foreach (BookEvent bookEvent in ordered)
        {
            Apply(book, entries, voidedEntries, voidedLoans, bookEvent);
            if (bookEvent.Change is EntryApproved approved && !approvals.ContainsKey(approved.EntryId))
            {
                approvals[approved.EntryId] = bookEvent;
            }

            switch (bookEvent.Change)
            {
                case EntryDeleted deleted:
                    deletions[deleted.EntryId] = bookEvent;
                    break;
                case EntryRestored restored:
                    deletions[restored.EntryId] = bookEvent;
                    break;
                case EntryPurged purge:
                    purged.Add(purge.EntryId);
                    break;
            }
        }

        foreach (string id in purged)
        {
            entries.Remove(id);
        }

        // Applied last, like approvals: a delete that synced before its entry still counts. The latest one wins.
        foreach ((string entryId, BookEvent deletion) in deletions)
        {
            if (deletion.Change is EntryDeleted deleted && entries.TryGetValue(entryId, out Entry? deletedEntry) && deletedEntry.CanBeDeleted)
            {
                deletedEntry.DeletedBy = deletion.Actor;
                deletedEntry.DeletedUtc = deletion.AtUtc;
                deletedEntry.DeleteReason = deleted.Reason;
            }
        }

        // An entry stamped before its author joined (a device clock running behind): go by the author's role now.
        foreach (Entry early in entries.Values.Where(e => !e.NeedsApproval && e.CreatedBy.Length > 0 && !e.AuthorKnown))
        {
            TeamMember? author = book.FindMember(early.CreatedBy);
            early.NeedsApproval = author != null && RoleRules.NeedsApproval(author.Role);
        }

        // Applied last, so an approval that synced before its entry still counts. The first approval wins.
        foreach ((string entryId, BookEvent approval) in approvals)
        {
            if (entries.TryGetValue(entryId, out Entry? approvedEntry) && approvedEntry.NeedsApproval)
            {
                approvedEntry.ApprovedBy = approval.Actor;
                approvedEntry.ApprovedUtc = approval.AtUtc;
            }
        }

        List<Entry> kept = entries.Values.Where(e => !voidedEntries.Contains(e.Id) && (e.LoanId == null || !voidedLoans.Contains(e.LoanId))).ToList();
        book.Entries.AddRange(kept.Where(e => !e.IsDeleted));
        book.DeletedEntries.AddRange(kept.Where(e => e.IsDeleted));
        book.SortEntries();
        return book;
    }

    private static void Apply(
        Book book,
        Dictionary<string, Entry> entries,
        HashSet<string> voidedEntries,
        HashSet<string> voidedLoans,
        BookEvent bookEvent)
    {
        switch (bookEvent.Change)
        {
            case EntryRecorded recorded:
                Entry entry = recorded.Entry.Clone();
                if (book.FindEntryCategory(entry.CategoryId) == null)
                {
                    entry.CategoryId = EntryCategory.OtherId;
                }

                // Who recorded it is whoever made the change (the server checks this), whatever the entry itself says.
                if (bookEvent.Actor.Length > 0)
                {
                    entry.CreatedBy = bookEvent.Actor;
                }

                TeamMember? author = book.FindMember(entry.CreatedBy);
                entry.AuthorKnown = author != null;
                entry.NeedsApproval = author != null && RoleRules.NeedsApproval(author.Role);
                entry.ApprovedBy = null;
                entry.ApprovedUtc = null;

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
                TeamMember savedMember = memberSaved.Member.Clone();
                savedMember.Active = true;
                Upsert(book.Members, savedMember, m => m.Id);
                break;

            case MemberRemoved memberRemoved:
                int memberIndex = book.Members.FindIndex(m => m.Id == memberRemoved.MemberId);
                if (memberIndex >= 0)
                {
                    TeamMember removed = book.Members[memberIndex].Clone();
                    removed.Active = false;
                    book.Members[memberIndex] = removed;
                }

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
