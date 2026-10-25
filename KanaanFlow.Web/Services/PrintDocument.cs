namespace KanaanFlow.Web.Services;

using System.Collections.Generic;

/// <summary>
/// A report or statement laid out for printing (and "Save as PDF").
/// </summary>
public sealed class PrintDocument
{
    public string Title { get; set; } = string.Empty;

    public List<string> MetaLines { get; } = new List<string>();

    public List<string> TotalsHeader { get; } = new List<string>();

    public List<List<string>> TotalsRows { get; } = new List<List<string>>();

    public List<string> EntriesHeader { get; } = new List<string>();

    public List<List<string>> EntriesRows { get; } = new List<List<string>>();
}
