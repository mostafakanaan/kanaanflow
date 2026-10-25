namespace KanaanFlow.Core.Finance;

using System;
using System.Collections.Generic;

/// <summary>
/// Which entries a report covers. Empty category sets mean "all".
/// </summary>
public sealed class ReportFilter
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public HashSet<string> EntryCategoryIds { get; } = new HashSet<string>();

    public HashSet<string> PeopleCategoryIds { get; } = new HashSet<string>();

    /// <summary>Only this contact's entries, or everyone when null.</summary>
    public string? ContactId { get; set; }

    /// <summary>Only entries this team member recorded, or everyone's when null.</summary>
    public string? CreatedBy { get; set; }

    public ApprovalFilter Approval { get; set; } = ApprovalFilter.All;
}

public enum ApprovalFilter
{
    All,

    /// <summary>Owners' entries an Accountant has not approved yet.</summary>
    Pending,

    /// <summary>Owners' entries an Accountant has approved.</summary>
    Approved
}
