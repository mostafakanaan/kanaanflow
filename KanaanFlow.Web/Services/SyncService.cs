namespace KanaanFlow.Web.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance.Events;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// Uploads this device's new events to the team's sync server and downloads everyone else's.
/// Events are append-only, so syncing never overwrites anyone's entries.
/// </summary>
public sealed class SyncService : IDisposable
{
    private const int PageSize = 500;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly BookService book;
    private readonly ApiClient api;
    private readonly ToastService toasts;
    private readonly IJSRuntime js;
    private readonly ILogger<SyncService> logger;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private DotNetObjectReference<SyncService>? selfRef;
    private CancellationTokenSource? debounce;
    private PeriodicTimer? timer;
    private bool online = true;
    private bool rerun;

    public SyncService(BookService book, ApiClient api, ToastService toasts, IJSRuntime js, ILogger<SyncService> logger)
    {
        this.book = book;
        this.api = api;
        this.toasts = toasts;
        this.js = js;
        this.logger = logger;
    }

    public SyncState State { get; private set; } = SyncState.LocalOnly;

    public bool IsOnline => online;

    /// <summary>
    /// True after signing in to an existing team book while this device still has its own unsynced changes:
    /// uploads wait until the person decides whether those changes belong in the team book.
    /// </summary>
    public bool HoldingUploads { get; private set; }

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

    /// <summary>Creates a new user on the team server (needs the master password). New users are Admins.</summary>
    public async Task RegisterAsync(string name, string password, string masterPassword)
    {
        // Keep the id of whoever used this device before, so their entries stay theirs.
        string memberId = book.Settings.LocalAccounts.FirstOrDefault()?.MemberId ?? BookService.NewId();
        ApiSession session = await api.RegisterAsync(name, password, masterPassword, memberId);
        await StartSessionAsync(session);
    }

    /// <summary>Signs in to the team book.</summary>
    public async Task SignInAsync(string name, string password)
    {
        ApiSession session = await api.LoginAsync(name, password);
        await StartSessionAsync(session);
    }

    /// <summary>Logs out on this device (and ends the server session). Unsynced changes stay until their author logs in again.</summary>
    public async Task LogOutAsync()
    {
        if (book.Settings.SignedIn)
        {
            try
            {
                await api.LogoutAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is ApiException || ex is TaskCanceledException)
            {
                logger.LogInformation(ex, "Sign-out request failed; logging out on this device anyway.");
            }
        }

        HoldingUploads = false;
        await book.LogOutAsync();
        UpdateIdleState();
    }

    /// <summary>Answers the held-uploads question: upload the changes as the logged-in member, or throw them away.</summary>
    public async Task ResolveHeldAsync(bool upload)
    {
        List<string> ids = book.ForeignUnsyncedEvents().Select(e => e.Id).ToList();
        if (upload)
        {
            await book.ClaimUnsyncedAsync(ids);
        }
        else
        {
            await book.DropUnsyncedAsync(ids);
        }

        HoldingUploads = false;
        await SyncNowAsync();
    }

    public async Task SyncNowAsync()
    {
        if (!book.Settings.SignedIn || !online)
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
            HoldingUploads = book.ForeignUnsyncedEvents().Count > 0;
            book.Settings.LastSyncUtc = DateTime.UtcNow;
            await book.SaveSettingsAsync();
            State = SyncState.Synced;
        }
        catch (ApiException ex) when (ex.Status == 401)
        {
            // Signed out on the server (password reset, signed out by an Admin, or removed from the team): log in again.
            HoldingUploads = false;
            await book.LogOutAsync();
            State = SyncState.LocalOnly;
            toasts.Show(book.T("signedOutNotice"));
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is TaskCanceledException || ex is ApiException || ex is InvalidOperationException)
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

    public void Dispose()
    {
        book.LocalChange -= OnLocalChange;
        debounce?.Cancel();
        timer?.Dispose();
        selfRef?.Dispose();
    }

    private async Task StartSessionAsync(ApiSession session)
    {
        book.Settings.Token = session.Token;
        book.Settings.PulledSeq = 0;
        book.Settings.LocalAccounts.Clear();
        await book.LogInAsync(session.Member.Id, session.Member.Role, localOnly: false);

        // A device-only team never uploads (the team lives on the server), so it shouldn't count as waiting.
        await book.DropUnsyncedAsync(book.UnsyncedEvents().Where(e => e.Change is MemberSaved or MemberRemoved).Select(e => e.Id).ToList());

        // Changes on this device by someone the team doesn't know wait for a decision (see ResolveHeldAsync).
        HoldingUploads = book.ForeignUnsyncedEvents().Count > 0;
        UpdateIdleState();
        _ = SyncNowAsync();
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
        if (!book.Settings.SignedIn)
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
        // Only the logged-in member's own changes: the server ties every change to whoever uploads it.
        IReadOnlyList<BookEvent> pending = book.MyUnsyncedEvents();
        int refused = 0;
        for (int start = 0; start < pending.Count; start += PageSize)
        {
            List<BookEvent> batch = pending.Skip(start).Take(PageSize).ToList();
            PushResult result = await api.PushAsync(batch);
            await book.MarkSyncedAsync(result.Accepted);

            // The server refuses changes this member's role doesn't allow, and team changes made on the device
            // (the team lives on the server). Those can never sync, so they are removed from this device.
            List<string> refusedIds = result.Rejected.Select(r => r.Id).ToList();
            await book.DropUnsyncedAsync(refusedIds);
            refused += result.Rejected.Count(r => r.Reason != "members-managed-by-server");
        }

        if (refused > 0)
        {
            toasts.Show(book.T("refusedChanges").Replace("{0}", refused.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private async Task PullAsync()
    {
        while (true)
        {
            List<(long Seq, BookEvent? Event)> rows = await api.PullAsync(book.Settings.PulledSeq, PageSize);
            await book.MergeRemoteAsync(rows.Where(r => r.Event != null).Select(r => r.Event!));
            if (rows.Count > 0)
            {
                book.Settings.PulledSeq = rows[rows.Count - 1].Seq;
            }

            if (rows.Count < PageSize)
            {
                return;
            }
        }
    }
}
