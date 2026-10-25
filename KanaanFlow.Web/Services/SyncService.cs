namespace KanaanFlow.Web.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance.Events;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// Uploads this device's new events and downloads the team's, through a Supabase table (see docs/sync-supabase.sql).
/// Events are append-only, so syncing never overwrites anyone's entries.
/// </summary>
public sealed class SyncService : IDisposable
{
    private const int PageSize = 500;

    // Server sequence numbers can commit slightly out of order; re-reading a small overlap avoids missing any.
    private const long PullOverlap = 100;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly BookService book;
    private readonly HttpClient http;
    private readonly IJSRuntime js;
    private readonly ILogger<SyncService> logger;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private DotNetObjectReference<SyncService>? selfRef;
    private CancellationTokenSource? debounce;
    private PeriodicTimer? timer;
    private bool online = true;
    private bool rerun;

    public SyncService(BookService book, HttpClient http, IJSRuntime js, ILogger<SyncService> logger)
    {
        this.book = book;
        this.http = http;
        this.js = js;
        this.logger = logger;
    }

    public SyncState State { get; private set; } = SyncState.LocalOnly;

    public bool IsOnline => online;

    public async Task StartAsync()
    {
        online = await js.InvokeAsync<bool>("kf.isOnline");
        selfRef = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("kf.watchOnline", selfRef);
        book.LocalChange += OnLocalChange;
        UpdateIdleState();
        _ = SyncNowAsync();
        _ = RunTimerAsync();
    }

    [JSInvokable]
    public Task OnOnlineChanged(bool isOnline)
    {
        online = isOnline;
        UpdateIdleState();
        book.NotifyChanged();
        return isOnline ? SyncNowAsync() : Task.CompletedTask;
    }

    public async Task SyncNowAsync()
    {
        if (!book.Settings.SyncConfigured || !online)
        {
            UpdateIdleState();
            book.NotifyChanged();
            return;
        }

        if (!await gate.WaitAsync(0))
        {
            rerun = true;
            return;
        }

        try
        {
            State = SyncState.Syncing;
            book.NotifyChanged();
            await PushAsync();
            await PullAsync();
            book.Settings.LastSyncUtc = DateTime.UtcNow;
            await book.SaveSettingsAsync();
            State = SyncState.Synced;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is TaskCanceledException || ex is InvalidOperationException)
        {
            logger.LogWarning(ex, "Sync failed.");
            State = online ? SyncState.Error : SyncState.Offline;
        }
        finally
        {
            gate.Release();
            book.NotifyChanged();
        }

        if (rerun && State == SyncState.Synced)
        {
            rerun = false;
            await SyncNowAsync();
        }
    }

    /// <summary>Saves new server settings and starts a fresh download of the book.</summary>
    public async Task ConfigureAsync(string url, string key, string bookCode)
    {
        book.Settings.SyncUrl = url.Trim().TrimEnd('/');
        book.Settings.SyncKey = key.Trim();
        book.Settings.BookCode = bookCode.Trim();
        book.Settings.PulledSeq = 0;
        await book.SaveSettingsAsync();
        await SyncNowAsync();
    }

    public async Task DisconnectAsync()
    {
        book.Settings.SyncUrl = string.Empty;
        book.Settings.SyncKey = string.Empty;
        book.Settings.BookCode = string.Empty;
        book.Settings.PulledSeq = 0;
        await book.SaveSettingsAsync();
        UpdateIdleState();
    }

    public void Dispose()
    {
        book.LocalChange -= OnLocalChange;
        debounce?.Cancel();
        timer?.Dispose();
        selfRef?.Dispose();
    }

    private void OnLocalChange()
    {
        debounce?.Cancel();
        CancellationTokenSource source = new CancellationTokenSource();
        debounce = source;
        _ = Task.Delay(TimeSpan.FromSeconds(1.5), source.Token).ContinueWith(
            t =>
            {
                if (!t.IsCanceled)
                {
                    _ = SyncNowAsync();
                }
            },
            TaskScheduler.Default);
    }

    private async Task RunTimerAsync()
    {
        timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync())
        {
            await SyncNowAsync();
        }
    }

    private void UpdateIdleState()
    {
        if (!book.Settings.SyncConfigured)
        {
            State = SyncState.LocalOnly;
        }
        else if (!online)
        {
            State = SyncState.Offline;
        }
        else if (State == SyncState.LocalOnly || State == SyncState.Offline)
        {
            State = book.PendingCount > 0 ? SyncState.Syncing : SyncState.Synced;
        }
    }

    private async Task PushAsync()
    {
        IReadOnlyList<BookEvent> pending = book.UnsyncedEvents();
        for (int start = 0; start < pending.Count; start += PageSize)
        {
            List<BookEvent> batch = pending.Skip(start).Take(PageSize).ToList();
            JsonArray rows = new JsonArray();
            foreach (BookEvent bookEvent in batch)
            {
                rows.Add(new JsonObject
                {
                    ["id"] = bookEvent.Id,
                    ["book_code"] = book.Settings.BookCode,
                    ["at"] = bookEvent.AtUtc.ToString("o", CultureInfo.InvariantCulture),
                    ["device_id"] = bookEvent.DeviceId,
                    ["payload"] = JsonNode.Parse(BookJsonContext.Serialize(bookEvent))
                });
            }

            using HttpRequestMessage request = NewRequest(HttpMethod.Post, "/rest/v1/kf_events?on_conflict=id");
            request.Headers.Add("Prefer", "resolution=ignore-duplicates,return=minimal");
            request.Content = new StringContent(rows.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await book.MarkSyncedAsync(batch.Select(e => e.Id).ToList());
        }
    }

    private async Task PullAsync()
    {
        long after = Math.Max(0, book.Settings.PulledSeq - PullOverlap);
        while (true)
        {
            string path = "/rest/v1/kf_events?select=seq,payload"
                + "&book_code=eq." + Uri.EscapeDataString(book.Settings.BookCode)
                + "&seq=gt." + after.ToString(CultureInfo.InvariantCulture)
                + "&order=seq.asc&limit=" + PageSize.ToString(CultureInfo.InvariantCulture);

            using HttpRequestMessage request = NewRequest(HttpMethod.Get, path);
            using HttpResponseMessage response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();

            List<BookEvent> incoming = new List<BookEvent>();
            int count = 0;
            using (JsonDocument doc = JsonDocument.Parse(body))
            {
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    count++;
                    after = Math.Max(after, row.GetProperty("seq").GetInt64());
                    BookEvent? bookEvent = ReadPayload(row.GetProperty("payload"));
                    if (bookEvent != null)
                    {
                        incoming.Add(bookEvent);
                    }
                }
            }

            await book.MergeRemoteAsync(incoming);
            book.Settings.PulledSeq = Math.Max(book.Settings.PulledSeq, after);
            if (count < PageSize)
            {
                return;
            }
        }
    }

    private BookEvent? ReadPayload(JsonElement payload)
    {
        try
        {
            return BookJsonContext.Deserialize(payload.GetRawText());
        }
        catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
        {
            logger.LogWarning(ex, "Skipped an unreadable event from the server.");
            return null;
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        HttpRequestMessage request = new HttpRequestMessage(method, book.Settings.SyncUrl + path);
        request.Headers.Add("apikey", book.Settings.SyncKey);
        request.Headers.Add("Authorization", "Bearer " + book.Settings.SyncKey);
        return request;
    }
}
