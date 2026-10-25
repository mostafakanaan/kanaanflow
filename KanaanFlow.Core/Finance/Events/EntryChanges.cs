namespace KanaanFlow.Core.Finance.Events;

/// <summary>Which changes are about one entry. The server and the app both use it to find what a purge removes.</summary>
public static class EntryChanges
{
    /// <summary>The entry a change is about, or null when it is about none.</summary>
    public static string? EntryIdOf(BookChange change) => change switch
    {
        EntryRecorded recorded => recorded.Entry.Id,
        EntryVoided voided => voided.EntryId,
        EntryApproved approved => approved.EntryId,
        EntryDeleted deleted => deleted.EntryId,
        EntryRestored restored => restored.EntryId,
        EntryPurged purged => purged.EntryId,
        _ => null
    };
}
