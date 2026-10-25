namespace KanaanFlow.Web.Services;

public enum SyncState
{
    /// <summary>Not signed in to a sync server: the book lives on this device only.</summary>
    LocalOnly,

    Offline,

    Syncing,

    Synced,

    Error
}
