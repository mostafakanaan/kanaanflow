namespace KanaanFlow.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using KanaanFlow.Sync.Policy;
using Xunit;

public class RolesAndApprovalTests
{
    private static readonly DateOnly Today = new DateOnly(2026, 10, 23);
    private static readonly Func<string, string?> SomeoneElse = _ => "other";
    private readonly List<BookEvent> events = new List<BookEvent>();
    private DateTime clock = new DateTime(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TeamRole.Admin)]
    [InlineData(TeamRole.Owner)]
    [InlineData(TeamRole.Accountant)]
    public void Managers_CanChangeTheBookAndUndoAnyonesEntries(TeamRole role)
    {
        Assert.Null(ChangePolicy.Check(role, "me", new EntryCategorySaved(), SomeoneElse, SomeoneElse));
        Assert.Null(ChangePolicy.Check(role, "me", new OpeningBalanceSet(), SomeoneElse, SomeoneElse));
        Assert.Null(ChangePolicy.Check(role, "me", new EntryVoided { EntryId = "e" }, SomeoneElse, SomeoneElse));
    }

    [Theory]
    [InlineData(TeamRole.Accountant, null)]
    [InlineData(TeamRole.Admin, null)]
    [InlineData(TeamRole.Owner, "accountant-only")]
    [InlineData(TeamRole.Staff, "accountant-only")]
    [InlineData(TeamRole.Viewer, "view-only")]
    public void OnlyAccountantsAndAdmins_CanApprove(TeamRole role, string? expected)
    {
        Assert.Equal(expected, ChangePolicy.Check(role, "me", new EntryApproved { EntryId = "e" }, SomeoneElse, SomeoneElse));
    }

    [Fact]
    public void OwnersAndAccountants_CannotTouchAdmins_AdminsCanTouchEveryone()
    {
        Assert.False(RoleRules.CanAssign(TeamRole.Owner, TeamRole.Admin));
        Assert.False(RoleRules.CanAssign(TeamRole.Accountant, TeamRole.Admin));
        Assert.False(RoleRules.CanEdit(TeamRole.Owner, TeamRole.Admin));
        Assert.True(RoleRules.CanAssign(TeamRole.Owner, TeamRole.Owner));
        Assert.True(RoleRules.CanAssign(TeamRole.Owner, TeamRole.Accountant));
        Assert.True(RoleRules.CanEdit(TeamRole.Owner, TeamRole.Accountant));

        // Admins can always change, and reset the password of, anyone, other Admins included.
        Assert.True(RoleRules.CanAssign(TeamRole.Admin, TeamRole.Admin));
        Assert.True(RoleRules.CanEdit(TeamRole.Admin, TeamRole.Admin));
        Assert.True(RoleRules.CanEdit(TeamRole.Admin, TeamRole.Owner));

        Assert.False(RoleRules.CanEdit(TeamRole.Viewer, TeamRole.Viewer));
        Assert.False(RoleRules.CanAssign(TeamRole.Staff, TeamRole.Viewer));
    }

    [Fact]
    public void Employee_IsNotOfferedYet()
    {
        Assert.False(RoleRules.CanAssign(TeamRole.Admin, TeamRole.Staff));
        Assert.False(RoleRules.IsAvailable(TeamRole.Staff));
    }

    [Fact]
    public void Entries_AreTiedToWhoeverMadeTheChange()
    {
        Member("owner", TeamRole.Owner);
        string id = Record("owner", createdBy: "someone-else");

        Entry entry = Project().Entries.Single(e => e.Id == id);

        Assert.Equal("owner", entry.CreatedBy);
    }

    [Fact]
    public void OwnersEntries_WaitForApproval_AndCountInBalancesEitherWay()
    {
        Member("owner", TeamRole.Owner);
        Member("acc", TeamRole.Accountant);
        string ownerEntry = Record("owner");
        string accountantEntry = Record("acc");

        Book book = Project();

        Assert.True(book.Entries.Single(e => e.Id == ownerEntry).IsPendingApproval);
        Assert.False(book.Entries.Single(e => e.Id == accountantEntry).NeedsApproval);
        Assert.Equal(200m, BookCalculator.CashBalances(book)[Currency.USD]);
    }

    [Fact]
    public void Approval_ShowsWhoApproved_EvenWhenItArrivesBeforeTheEntry()
    {
        Member("owner", TeamRole.Owner);
        Member("acc", TeamRole.Accountant);
        string id = Record("owner");
        events.Single(e => e.Change is EntryRecorded).AtUtc = clock.AddMinutes(5);
        Add("acc", new EntryApproved { EntryId = id });

        Entry entry = Project().Entries.Single();

        Assert.Equal("acc", entry.ApprovedBy);
        Assert.False(entry.IsPendingApproval);
    }

    [Fact]
    public void EntryStampedBeforeItsAuthorJoined_StillNeedsApproval()
    {
        string id = Record("owner");
        Member("owner", TeamRole.Owner);

        Assert.True(Project().Entries.Single(e => e.Id == id).IsPendingApproval);
    }

    [Fact]
    public void Reports_FilterByWhoRecordedAndByApproval()
    {
        Member("owner", TeamRole.Owner);
        Member("acc", TeamRole.Accountant);
        string approved = Record("owner");
        Record("owner");
        Record("acc");
        Add("acc", new EntryApproved { EntryId = approved });
        Book book = Project();

        ReportFilter byOwner = new ReportFilter { From = Today, To = Today, CreatedBy = "owner" };
        ReportFilter pending = new ReportFilter { From = Today, To = Today, Approval = ApprovalFilter.Pending };
        ReportFilter done = new ReportFilter { From = Today, To = Today, Approval = ApprovalFilter.Approved };

        Assert.Equal(2, BookCalculator.Report(book, byOwner).Entries.Count);
        Assert.Single(BookCalculator.Report(book, pending).Entries);
        Assert.Equal(approved, BookCalculator.Report(book, done).Entries.Single().Id);
    }

    [Fact]
    public void RemovedMembers_StillNameTheirEntries()
    {
        Member("owner", TeamRole.Owner);
        Record("owner");
        Add("admin", new MemberRemoved { MemberId = "owner" });

        Book book = Project();

        Assert.Equal("owner", book.FindMember("owner")!.Name);
        Assert.False(book.FindMember("owner")!.Active);
        Assert.Empty(book.ActiveMembers);
    }

    [Fact]
    public void OlderEvents_WithTheOriginalRoles_StillLoad()
    {
        string json = "{\"id\":\"e1\",\"atUtc\":\"2026-01-01T00:00:00Z\",\"deviceId\":\"server\",\"actor\":\"m1\","
            + "\"change\":{\"kind\":\"member.saved\",\"member\":{\"id\":\"m1\",\"name\":\"Mostafa\",\"role\":\"Viewer\",\"canRecord\":false,\"canManage\":false}}}";

        BookEvent? bookEvent = BookJsonContext.Deserialize(json);

        Assert.Equal(TeamRole.Viewer, Assert.IsType<MemberSaved>(bookEvent!.Change).Member.Role);
    }

    private Book Project() => BookProjector.Project(events);

    private void Member(string id, TeamRole role) => Add("server", new MemberSaved { Member = new TeamMember { Id = id, Name = id, Role = role } });

    private void Add(string actor, BookChange change)
    {
        clock = clock.AddSeconds(1);
        events.Add(new BookEvent { Id = Guid.NewGuid().ToString("N"), AtUtc = clock, DeviceId = "d1", Actor = actor, Change = change });
    }

    private string Record(string actor, string createdBy = "")
    {
        string id = Guid.NewGuid().ToString("N");
        Add(actor, new EntryRecorded
        {
            Entry = new Entry { Id = id, Type = EntryType.In, Amount = 100m, Currency = Currency.USD, Date = Today, CreatedBy = createdBy }
        });
        return id;
    }
}
