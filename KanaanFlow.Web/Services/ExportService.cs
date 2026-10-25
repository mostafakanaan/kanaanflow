namespace KanaanFlow.Web.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance;
using Microsoft.JSInterop;

/// <summary>
/// Turns reports into files: Excel workbooks to download or share, and print layouts for PDF.
/// </summary>
public sealed class ExportService
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly BookService book;
    private readonly ToastService toasts;
    private readonly IJSRuntime js;

    public ExportService(BookService book, ToastService toasts, IJSRuntime js)
    {
        this.book = book;
        this.toasts = toasts;
        this.js = js;
    }

    /// <summary>Raised when <see cref="Current"/> changes, so the print area can render it.</summary>
    public event Action? PrintChanged;

    public PrintDocument? Current { get; private set; }

    public async Task ExportReportXlsxAsync(ReportResult report)
    {
        bool rtl = book.IsRtl;
        List<IReadOnlyList<object?>> entries = new List<IReadOnlyList<object?>>
        {
            new object?[] { book.T("date"), book.T("time"), book.T("typeCol"), book.T("personCol"), book.T("category"), book.T("noteCol"), book.T("inTotal"), book.T("outTotal"), book.T("currency"), book.T("recordedBy"), book.T("approval") }
        };
        foreach (Entry entry in report.Entries)
        {
            entries.Add(new object?[]
            {
                Fmt.Iso(entry.Date),
                entry.Time,
                entry.Type == EntryType.In ? book.T("received") : book.T("paid"),
                book.ContactName(entry.ContactId),
                book.EntryCategoryName(entry.CategoryId),
                entry.Note,
                entry.Type == EntryType.In ? entry.Amount : null,
                entry.Type == EntryType.Out ? entry.Amount : null,
                Fmt.LongName(entry.Currency, book.Language),
                book.MemberName(entry.CreatedBy),
                ApprovalText(entry)
            });
        }

        List<IReadOnlyList<object?>> summary = new List<IReadOnlyList<object?>>
        {
            new object?[] { book.T("currency"), book.T("inTotal"), book.T("outTotal"), book.T("net") }
        };
        foreach (CurrencyTotals totals in report.Totals)
        {
            summary.Add(new object?[] { Fmt.LongName(totals.Currency, book.Language), totals.In, totals.Out, totals.Net });
        }

        XlsxWriter writer = new XlsxWriter();
        writer.AddSheet(book.T("summary"), summary, rtl);
        writer.AddSheet(book.T("history"), entries, rtl);
        await SaveAsync("KanaanFlow_report_" + Fmt.Iso(book.Today) + ".xlsx", XlsxMime, writer.ToBytes());
    }

    public async Task PrintReportAsync(ReportResult report, string title, IEnumerable<string> metaLines)
    {
        PrintDocument doc = new PrintDocument { Title = title };
        doc.MetaLines.AddRange(metaLines);
        doc.MetaLines.Add(book.T("printedOn") + ": " + Fmt.LongDate(book.Today, book.Language) + " · " + report.Entries.Count + " " + book.T("entriesN"));
        AddTotals(doc, report.Totals);
        doc.EntriesHeader.AddRange(new[] { book.T("date"), book.T("typeCol"), book.T("personCol"), book.T("category"), book.T("noteCol"), book.T("amount"), book.T("recordedBy"), book.T("approval") });
        foreach (Entry entry in report.Entries)
        {
            doc.EntriesRows.Add(new List<string>
            {
                book.DateText(entry.Date) + (entry.Time.Length > 0 ? " " + entry.Time : string.Empty),
                entry.Type == EntryType.In ? book.T("received") : book.T("paid"),
                book.ContactName(entry.ContactId),
                book.EntryCategoryName(entry.CategoryId),
                entry.Note,
                Fmt.Signed(entry.Amount, entry.Type == EntryType.In) + " " + book.Sym(entry.Currency),
                book.MemberName(entry.CreatedBy),
                ApprovalText(entry)
            });
        }

        await PrintAsync(doc);
    }

    public async Task PrintAsync(PrintDocument doc)
    {
        Current = doc;
        PrintChanged?.Invoke();
        await Task.Yield();
        await js.InvokeVoidAsync("kf.print");
        toasts.Show(book.T("printHint"));
    }

    public void AddTotals(PrintDocument doc, IEnumerable<CurrencyTotals> totals)
    {
        doc.TotalsHeader.AddRange(new[] { book.T("currency"), book.T("inTotal"), book.T("outTotal"), book.T("net") });
        foreach (CurrencyTotals row in totals)
        {
            doc.TotalsRows.Add(new List<string> { book.Sym(row.Currency), Fmt.Money(row.In), Fmt.Money(row.Out), Fmt.Net(row.Net) });
        }
    }

    /// <summary>Downloads every change in the book as JSON: a full backup that any device can read.</summary>
    public Task ExportBackupAsync()
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(book.ExportEventsJson());
        return SaveAsync("KanaanFlow_backup_" + Fmt.Iso(book.Today) + ".json", "application/json", bytes);
    }

    /// <summary>"Approved by …", "Awaiting approval", or empty for entries that don't need approval.</summary>
    public string ApprovalText(Entry entry)
    {
        if (!entry.NeedsApproval)
        {
            return string.Empty;
        }

        return entry.ApprovedBy != null ? book.T("approvedBy") + " " + book.MemberName(entry.ApprovedBy) : book.T("awaitingApproval");
    }

    private async Task SaveAsync(string name, string mime, byte[] bytes)
    {
        await js.InvokeVoidAsync("kf.download", name, mime, bytes);
        bool canShare = await js.InvokeAsync<bool>("kf.canShareFiles");
        toasts.Show(
            book.T("fileReady") + ": " + name,
            share: canShare ? async () => { await js.InvokeAsync<bool>("kf.share", name, mime, bytes); toasts.Clear(); } : null);
    }
}
