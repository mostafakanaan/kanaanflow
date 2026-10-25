namespace KanaanFlow.Web.Services;

public enum SyncState
{
    /// <summary>No sync server set up: the book lives on this device only.</summary>
    LocalOnly,

    Offline,

    Syncing,

    Synced,

    Error
}
