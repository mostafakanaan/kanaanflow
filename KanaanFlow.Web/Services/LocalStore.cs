namespace KanaanFlow.Web.Services;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;

/// <summary>
/// The device's own copy of the book, kept in the browser's IndexedDB so it works with no internet.
/// </summary>
public sealed class LocalStore
{
    private readonly IJSRuntime js;

    public LocalStore(IJSRuntime js)
    {
        this.js = js;
    }

    public async Task<List<StoredEvent>> GetEventsAsync()
    {
        StoredEvent[]? rows = await js.InvokeAsync<StoredEvent[]?>("kf.getEvents");
        return rows == null ? new List<StoredEvent>() : new List<StoredEvent>(rows);
    }

    public ValueTask PutEventsAsync(IReadOnlyList<StoredEvent> rows) => js.InvokeVoidAsync("kf.putEvents", rows);

    public ValueTask DeleteEventsAsync(IReadOnlyList<string> ids) => js.InvokeVoidAsync("kf.deleteEvents", ids);

    public ValueTask ClearEventsAsync() => js.InvokeVoidAsync("kf.clearEvents");

    public ValueTask MarkSyncedAsync(IReadOnlyList<string> ids) => js.InvokeVoidAsync("kf.markSynced", ids);

    public ValueTask<string?> GetMetaAsync(string key) => js.InvokeAsync<string?>("kf.getMeta", key);

    public ValueTask SetMetaAsync(string key, string value) => js.InvokeVoidAsync("kf.setMeta", key, value);

    /// <summary>Asks the browser not to clear our storage when space runs low (important on iPhone).</summary>
    public ValueTask<bool> RequestPersistenceAsync() => js.InvokeAsync<bool>("kf.persist");
}
