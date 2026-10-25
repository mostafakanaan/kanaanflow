namespace KanaanFlow.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using KanaanFlow.Sync.Policy;
using Xunit;

public class DeleteTests
{
    private static readonly DateOnly Today = new DateOnly(2026, 10, 25);
    private static readonly Func<string, string?> Anyone = _ => "other";
    private readonly List<BookEvent> events = new List<BookEvent>();
    private DateTime clock = new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DeletedEntry_LeavesBalancesAndReports_AndShowsInDeletedList()
    {
        string kept = Record(100m);
        string wrong = Record(40m);
        Add("acc", new EntryDeleted { EntryId = wrong, Reason = "twice" });

        Book book = Project();

        Assert.Equal(100m, BookCalculator.CashBalances(book)[Currency.USD]);
        Assert.Equal(kept, BookCalculator.Report(book, new ReportFilter { From = Today, To = Today }).Entries.Single().Id);
        Entry deleted = book.DeletedEntries.Single();
        Assert.Equal(wrong, deleted.Id);
        Assert.Equal("acc", deleted.DeletedBy);
        Assert.Equal("twice", deleted.DeleteReason);
    }

    [Fact]
    public void Restore_BringsTheEntryBack()
    {
        string id = Record(40m);
        Add("acc", new EntryDeleted { EntryId = id });
        Add("acc", new EntryRestored { EntryId = id });

        Book book = Project();

        Assert.Equal(id, book.Entries.Single().Id);
        Assert.Empty(book.DeletedEntries);
    }

    [Fact]
    public void Purge_RemovesTheEntryEverywhere_EvenAfterARestore()
    {
        string id = Record(40m);
        Add("acc", new EntryDeleted { EntryId = id });
        Add("admin", new EntryPurged { EntryId = id });
        Add("acc", new EntryRestored { EntryId = id });

        Book book = Project();

        Assert.Empty(book.Entries);
        Assert.Empty(book.DeletedEntries);
        Assert.Equal(0m, BookCalculator.CashBalances(book)[Currency.USD]);
    }

    [Fact]
    public void LoanPrincipal_CannotBeDeletedOnItsOwn()
    {
        string id = Guid.NewGuid().ToString("N");
        Add("owner", new EntryRecorded { Entry = new Entry { Id = id, Type = EntryType.Out, Amount = 500m, Currency = Currency.USD, Date = Today, LoanId = "l1", IsLoanPrincipal = true } });
        Add("acc", new EntryDeleted { EntryId = id });

        Assert.Single(Project().Entries);
    }

    [Theory]
    [InlineData(TeamRole.Owner, "accountant-only", "admin-only")]
    [InlineData(TeamRole.Accountant, null, "admin-only")]
    [InlineData(TeamRole.Admin, null, null)]
    [InlineData(TeamRole.Viewer, "view-only", "view-only")]
    public void OnlyAccountantsDelete_AndOnlyAdminsPurge(TeamRole role, string? delete, string? purge)
    {
        Assert.Equal(delete, ChangePolicy.Check(role, "me", new EntryDeleted { EntryId = "e" }, Anyone, Anyone));
        Assert.Equal(delete, ChangePolicy.Check(role, "me", new EntryRestored { EntryId = "e" }, Anyone, Anyone));
        Assert.Equal(purge, ChangePolicy.Check(role, "me", new EntryPurged { EntryId = "e" }, Anyone, Anyone));
    }

    private Book Project() => BookProjector.Project(events);

    private void Add(string actor, BookChange change)
    {
        clock = clock.AddSeconds(1);
        events.Add(new BookEvent { Id = Guid.NewGuid().ToString("N"), AtUtc = clock, DeviceId = "d1", Actor = actor, Change = change });
    }

    private string Record(decimal amount)
    {
        string id = Guid.NewGuid().ToString("N");
        Add("owner", new EntryRecorded { Entry = new Entry { Id = id, Type = EntryType.In, Amount = amount, Currency = Currency.USD, Date = Today } });
        return id;
    }
}
