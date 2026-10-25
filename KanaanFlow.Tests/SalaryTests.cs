namespace KanaanFlow.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using KanaanFlow.Sync.Policy;
using Xunit;

public class SalaryTests
{
    private static readonly DateOnly Today = new DateOnly(2026, 10, 25);
    private static readonly Func<string, string?> Anyone = _ => "other";
    private readonly List<BookEvent> events = new List<BookEvent>();
    private DateTime clock = new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Months_RunFromTheFirstSalaryToThisMonth_WithPaidPartialAndUnpaid()
    {
        Salary("k", "2026-08", 500m);
        Pay("k", "2026-08", 500m);
        Pay("k", "2026-09", 200m);

        List<SalaryMonthStatus> months = SalaryCalculator.Months(Project(), "k", Today);

        Assert.Equal(new[] { "2026-10", "2026-09", "2026-08" }, months.Select(m => m.Month));
        Assert.Equal(SalaryState.Unpaid, months[0].State);
        Assert.Equal(SalaryState.Partial, months[1].State);
        Assert.Equal(300m, months[1].Remaining);
        Assert.Equal(SalaryState.Paid, months[2].State);
        Assert.Equal(800m, SalaryCalculator.Owed(Project(), "k", Today)[Currency.USD]);
    }

    [Fact]
    public void ARaise_AppliesFromItsMonth_AndStoppingEndsTheSalary()
    {
        Salary("k", "2026-07", 400m);
        Salary("k", "2026-09", 450m);
        Salary("k", "2026-10", 0m);
        Book book = Project();

        Assert.Equal(400m, SalaryCalculator.MonthOf(book, "k", "2026-08")!.Due);
        Assert.Equal(450m, SalaryCalculator.MonthOf(book, "k", "2026-09")!.Due);
        Assert.Null(SalaryCalculator.MonthOf(book, "k", "2026-10"));
        Assert.Equal(3, SalaryCalculator.Months(book, "k", Today).Count);
    }

    [Fact]
    public void SalaryPayments_AreOrdinaryPaidEntries_InTheBalances()
    {
        Salary("k", "2026-10", 500m);
        Pay("k", "2026-10", 500m);

        Assert.Equal(-500m, BookCalculator.CashBalances(Project())[Currency.USD]);
    }

    [Theory]
    [InlineData(TeamRole.Admin, null)]
    [InlineData(TeamRole.Owner, null)]
    [InlineData(TeamRole.Accountant, null)]
    [InlineData(TeamRole.Staff, "salaries-managers-only")]
    [InlineData(TeamRole.Viewer, "view-only")]
    public void OnlyAdminsOwnersAndAccountants_ManageSalaries(TeamRole role, string? expected)
    {
        SalarySet set = new SalarySet { Salary = new SalaryPeriod { ContactId = "k", FromMonth = "2026-10", Amount = 1m } };
        EntryRecorded pay = new EntryRecorded { Entry = new Entry { Id = "e", SalaryMonth = "2026-10" } };

        Assert.Equal(expected, ChangePolicy.Check(role, "me", set, Anyone, Anyone));
        Assert.Equal(expected, ChangePolicy.Check(role, "me", pay, Anyone, Anyone));
    }

    private Book Project() => BookProjector.Project(events);

    private void Add(BookChange change)
    {
        clock = clock.AddSeconds(1);
        events.Add(new BookEvent { Id = Guid.NewGuid().ToString("N"), AtUtc = clock, DeviceId = "d1", Actor = "acc", Change = change });
    }

    private void Salary(string contactId, string from, decimal amount) =>
        Add(new SalarySet { Salary = new SalaryPeriod { ContactId = contactId, FromMonth = from, Amount = amount, Currency = Currency.USD } });

    private void Pay(string contactId, string month, decimal amount) =>
        Add(new EntryRecorded
        {
            Entry = new Entry { Id = Guid.NewGuid().ToString("N"), Type = EntryType.Out, Amount = amount, Currency = Currency.USD, ContactId = contactId, Date = Today, SalaryMonth = month }
        });
}
