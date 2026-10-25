namespace KanaanFlow.Web.Services;

/// <summary>
/// One event row as kept in IndexedDB.
/// </summary>
public sealed class StoredEvent
{
    public string Id { get; set; } = string.Empty;

    public string Json { get; set; } = string.Empty;

    public bool Synced { get; set; }
}
