namespace KanaanFlow.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using Xunit;

public class BookTests
{
    private static readonly DateOnly Today = new DateOnly(2026, 10, 23);
    private readonly List<BookEvent> events = new List<BookEvent>();
    private DateTime clock = new DateTime(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CashBalances_AddOpeningInOutAndExchange()
    {
        Add(new OpeningBalanceSet { Currency = Currency.USD, Amount = 4200m });
        Record(EntryType.In, 1500m, Currency.USD);
        Record(EntryType.Out, 350m, Currency.USD);
        Add(new EntryRecorded
        {
            Entry = new Entry { Id = "fx", Type = EntryType.Exchange, Amount = 100m, Currency = Currency.USD, ToAmount = 4150m, ToCurrency = Currency.TRY, Rate = 41.5m, Date = Today }
        });

        Dictionary<Currency, decimal> cash = BookCalculator.CashBalances(Project());

        Assert.Equal(5250m, cash[Currency.USD]);
        Assert.Equal(4150m, cash[Currency.TRY]);
        Assert.Equal(0m, cash[Currency.SYP]);
    }

    [Fact]
    public void VoidedEntry_IsUndoneButKeptInLog()
    {
        string id = Record(EntryType.In, 600m, Currency.EUR);
        Add(new EntryVoided { EntryId = id });

        Book book = Project();

        Assert.Empty(book.Entries);
        Assert.Equal(2, events.Count);
        Assert.Equal(0m, BookCalculator.CashBalances(book)[Currency.EUR]);
    }

    [Fact]
    public void LoanRepayments_ReduceRemainingAndMoveCash()
    {
        Add(new ContactSaved { Contact = new Contact { Id = "c4", Name = "Khaled", CategoryId = "employee" } });
        Add(new LoanOpened { Loan = new Loan { Id = "l1", Direction = LoanDirection.Lent, ContactId = "c4", Amount = 500m, Currency = Currency.USD, DueDate = Today.AddDays(-1) } });
        Record(EntryType.Out, 500m, Currency.USD, "c4", "loan", loanId: "l1", principal: true);
        Record(EntryType.In, 150m, Currency.USD, "c4", "loan", loanId: "l1");

        Book book = Project();
        LoanStatus status = BookCalculator.LoanStatusOf(book, book.Loans.Single(), Today);

        Assert.Equal(350m, status.Remaining);
        Assert.Equal(30, status.PercentRepaid);
        Assert.True(status.IsOverdue);
        Assert.Single(status.Payments);
        Assert.Equal(-350m, BookCalculator.CashBalances(book)[Currency.USD]);
        Assert.Equal(350m, BookCalculator.LoanTotals(book, LoanDirection.Lent, Today)[Currency.USD]);
    }

    [Fact]
    public void VoidedLoan_RemovesLoanAndItsEntries()
    {
        Add(new LoanOpened { Loan = new Loan { Id = "l1", Direction = LoanDirection.Borrowed, ContactId = "c1", Amount = 300m, Currency = Currency.USD } });
        Record(EntryType.In, 300m, Currency.USD, "c1", "loan", loanId: "l1", principal: true);
        Add(new LoanVoided { LoanId = "l1" });

        Book book = Project();

        Assert.Empty(book.Loans);
        Assert.Empty(book.Entries);
    }

    [Fact]
    public void RemovedCategory_MovesEntriesToOther()
    {
        Record(EntryType.Out, 10m, Currency.SYP, categoryId: "rent");
        Add(new EntryCategoryRemoved { CategoryId = "rent" });
        Add(new EntryCategoryRemoved { CategoryId = EntryCategory.OtherId });

        Book book = Project();

        Assert.Equal(EntryCategory.OtherId, book.Entries.Single().CategoryId);
        Assert.Null(book.FindEntryCategory("rent"));
        Assert.NotNull(book.FindEntryCategory(EntryCategory.OtherId));
    }

    [Fact]
    public void Projection_IsTheSameWhateverOrderEventsArrive()
    {
        Add(new ContactSaved { Contact = new Contact { Id = "c1", Name = "Old" } });
        Add(new ContactSaved { Contact = new Contact { Id = "c1", Name = "New" } });
        Record(EntryType.In, 5m, Currency.USD);

        List<BookEvent> reversed = Enumerable.Reverse(events).ToList();

        Assert.Equal("New", BookProjector.Project(reversed).Contacts.Single().Name);
        Assert.Equal(Project().Entries.Count, BookProjector.Project(reversed).Entries.Count);
    }

    [Fact]
    public void Report_FiltersByPeriodCategoryAndPeopleCategory()
    {
        Add(new ContactSaved { Contact = new Contact { Id = "c1", Name = "Rani", CategoryId = "customer" } });
        Add(new ContactSaved { Contact = new Contact { Id = "c2", Name = "Textile", CategoryId = "supplier" } });
        Record(EntryType.In, 1500m, Currency.USD, "c1", "sales");
        Record(EntryType.Out, 1200m, Currency.USD, "c2", "yarn");
        Record(EntryType.Out, 99m, Currency.USD, "c2", "yarn", date: Today.AddMonths(-1));

        Book book = Project();
        ReportFilter all = new ReportFilter { From = new DateOnly(2026, 10, 1), To = Today };
        ReportResult result = BookCalculator.Report(book, all);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(300m, result.Totals.Single().Net);

        ReportFilter suppliers = new ReportFilter { From = new DateOnly(2026, 10, 1), To = Today };
        suppliers.PeopleCategoryIds.Add("supplier");
        Assert.Equal(1200m, BookCalculator.Report(book, suppliers).Totals.Single().Out);

        ReportFilter sales = new ReportFilter { From = new DateOnly(2026, 10, 1), To = Today };
        sales.EntryCategoryIds.Add("sales");
        Assert.Equal(1500m, BookCalculator.Report(book, sales).Totals.Single().In);
    }

    [Fact]
    public void Events_RoundTripThroughJson()
    {
        string id = Record(EntryType.In, 12.5m, Currency.TRY, note: "دفعة");
        BookEvent original = events.Single();

        BookEvent? copy = BookJsonContext.Deserialize(BookJsonContext.Serialize(original));

        Assert.NotNull(copy);
        EntryRecorded recorded = Assert.IsType<EntryRecorded>(copy!.Change);
        Assert.Equal(id, recorded.Entry.Id);
        Assert.Equal(12.5m, recorded.Entry.Amount);
        Assert.Equal("دفعة", recorded.Entry.Note);
        Assert.Contains("\"kind\":\"entry.recorded\"", BookJsonContext.Serialize(original));
    }

    private Book Project() => BookProjector.Project(events);

    private void Add(BookChange change)
    {
        clock = clock.AddSeconds(1);
        events.Add(new BookEvent { Id = Guid.NewGuid().ToString("N"), AtUtc = clock, DeviceId = "d1", Change = change });
    }

    private string Record(
        EntryType type,
        decimal amount,
        Currency currency,
        string? contactId = null,
        string categoryId = "other",
        string? loanId = null,
        bool principal = false,
        DateOnly? date = null,
        string note = "")
    {
        string id = Guid.NewGuid().ToString("N");
        Add(new EntryRecorded
        {
            Entry = new Entry
            {
                Id = id, Type = type, Amount = amount, Currency = currency, ContactId = contactId, CategoryId = categoryId,
                LoanId = loanId, IsLoanPrincipal = principal, Date = date ?? Today, Note = note, CreatedUtc = clock
            }
        });
        return id;
    }
}
