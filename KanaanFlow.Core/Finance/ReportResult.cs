namespace KanaanFlow.Core.Finance;

using System.Collections.Generic;

public sealed class ReportResult
{
    public ReportResult(ReportFilter filter, IReadOnlyList<Entry> entries, IReadOnlyList<CurrencyTotals> totals)
    {
        Filter = filter;
        Entries = entries;
        Totals = totals;
    }

    public ReportFilter Filter { get; }

    /// <summary>Matching received and paid entries, newest first. Exchanges are not part of reports.</summary>
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>One row per currency that has activity.</summary>
    public IReadOnlyList<CurrencyTotals> Totals { get; }
}
