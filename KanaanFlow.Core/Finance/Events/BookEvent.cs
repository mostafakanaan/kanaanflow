namespace KanaanFlow.Core.Finance.Events;

using System;

/// <summary>
/// A change plus who made it, where and when. The event log is the book's source of truth and what syncs between devices.
/// </summary>
public sealed class BookEvent
{
    public string Id { get; set; } = string.Empty;

    public DateTime AtUtc { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    /// <summary>The team member id that made the change.</summary>
    public string Actor { get; set; } = string.Empty;

    public BookChange Change { get; set; } = new EntryVoided();
}
