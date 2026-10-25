namespace KanaanFlow.Web.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// The app's single source of state: the event log on this device, the book rebuilt from it, and device settings.
/// Every change is written to local storage first, so nothing is lost without internet.
/// </summary>
public sealed class BookService
{
    private const string SettingsKey = "settings";

    private readonly LocalStore store;
    private readonly ILogger<BookService> logger;
    private readonly Dictionary<string, BookEvent> events = new Dictionary<string, BookEvent>();
    private readonly HashSet<string> unsynced = new HashSet<string>();
    private DateTime lastEventUtc = DateTime.MinValue;

    public BookService(LocalStore store, ILogger<BookService> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    /// <summary>Raised after the book or settings change.</summary>
    public event Action? Changed;

    /// <summary>Raised after a change made on this device, so sync can upload it.</summary>
    public event Action? LocalChange;

    public Book Book { get; private set; } = new Book();

    public DeviceSettings Settings { get; private set; } = new DeviceSettings();

    public string Language => Settings.Language == "en" ? "en" : "ar";

    public bool IsRtl => Language == "ar";

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public int PendingCount => unsynced.Count;

    public TeamMember? Me => Book.FindMember(Settings.MemberId);

    public bool CanRecord => Me == null || Me.CanRecord;

    public bool CanManage => Me == null || Me.CanManage;

    public string T(string key)
    {
        Dictionary<string, string> table = Language == "en" ? Strings.En : Strings.Ar;
        return table.TryGetValue(key, out string? text) ? text : key;
    }

    public async Task LoadAsync()
    {
        string? settingsJson = await store.GetMetaAsync(SettingsKey);
        if (!string.IsNullOrEmpty(settingsJson))
        {
            try
            {
                Settings = JsonSerializer.Deserialize(settingsJson, WebJsonContext.Default.DeviceSettings) ?? new DeviceSettings();
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Device settings could not be read; starting fresh.");
                Settings = new DeviceSettings();
            }
        }

        if (string.IsNullOrEmpty(Settings.DeviceId))
        {
            Settings.DeviceId = NewId();
            await SaveSettingsAsync();
        }

        foreach (StoredEvent row in await store.GetEventsAsync())
        {
            BookEvent? bookEvent = TryRead(row.Json);
            if (bookEvent == null)
            {
                continue;
            }

            events[bookEvent.Id] = bookEvent;
            if (!row.Synced)
            {
                unsynced.Add(bookEvent.Id);
            }

            if (bookEvent.DeviceId == Settings.DeviceId && bookEvent.AtUtc > lastEventUtc)
            {
                lastEventUtc = bookEvent.AtUtc;
            }
        }

        Rebuild();
        _ = store.RequestPersistenceAsync().AsTask();
    }

    /// <summary>Appends changes to the log as one step and returns the ids of the new events.</summary>
    public async Task<IReadOnlyList<string>> RecordAsync(params BookChange[] changes)
    {
        List<StoredEvent> rows = new List<StoredEvent>();
        List<string> ids = new List<string>();
        foreach (BookChange change in changes)
        {
            DateTime at = DateTime.UtcNow;
            if (at <= lastEventUtc)
            {
                at = lastEventUtc.AddTicks(1);
            }

            lastEventUtc = at;
            BookEvent bookEvent = new BookEvent
            {
                Id = NewId(),
                AtUtc = at,
                DeviceId = Settings.DeviceId,
                Actor = Settings.MemberId,
                Change = change
            };

            events[bookEvent.Id] = bookEvent;
            unsynced.Add(bookEvent.Id);
            ids.Add(bookEvent.Id);
            rows.Add(new StoredEvent { Id = bookEvent.Id, Json = BookJsonContext.Serialize(bookEvent), Synced = false });
        }

        await store.PutEventsAsync(rows);
        Rebuild();
        Changed?.Invoke();
        LocalChange?.Invoke();
        return ids;
    }

    /// <summary>Adds events that arrived from other devices. Returns how many were new.</summary>
    public async Task<int> MergeRemoteAsync(IEnumerable<BookEvent> incoming)
    {
        List<StoredEvent> rows = new List<StoredEvent>();
        foreach (BookEvent bookEvent in incoming)
        {
            if (string.IsNullOrEmpty(bookEvent.Id) || events.ContainsKey(bookEvent.Id))
            {
                continue;
            }

            events[bookEvent.Id] = bookEvent;
            rows.Add(new StoredEvent { Id = bookEvent.Id, Json = BookJsonContext.Serialize(bookEvent), Synced = true });
        }

        if (rows.Count > 0)
        {
            await store.PutEventsAsync(rows);
            Rebuild();
            Changed?.Invoke();
        }

        return rows.Count;
    }

    public IReadOnlyList<BookEvent> UnsyncedEvents()
    {
        return unsynced.Select(id => events[id]).OrderBy(e => e.AtUtc).ToList();
    }

    public async Task MarkSyncedAsync(IReadOnlyList<string> ids)
    {
        await store.MarkSyncedAsync(ids);
        foreach (string id in ids)
        {
            unsynced.Remove(id);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Removes events from this device that will never be synced: ones the server refused, or local changes
    /// the person chose not to upload. Only unsynced events can be dropped.
    /// </summary>
    public async Task DropUnsyncedAsync(IReadOnlyList<string> ids)
    {
        List<string> drop = ids.Where(unsynced.Contains).ToList();
        if (drop.Count == 0)
        {
            return;
        }

        await store.DeleteEventsAsync(drop);
        foreach (string id in drop)
        {
            events.Remove(id);
            unsynced.Remove(id);
        }

        Rebuild();
        Changed?.Invoke();
    }

    /// <summary>Unsynced changes that would add to a shared book (team changes are managed by the server).</summary>
    public int PendingBookChanges => unsynced.Count(id => events[id].Change is not MemberSaved and not MemberRemoved);

    public async Task SaveSettingsAsync()
    {
        await store.SetMetaAsync(SettingsKey, JsonSerializer.Serialize(Settings, WebJsonContext.Default.DeviceSettings));
        Changed?.Invoke();
    }

    /// <summary>Lets other services (sync status, toasts) ask the UI to redraw.</summary>
    public void NotifyChanged() => Changed?.Invoke();

    public string ContactName(string? contactId) => Book.FindContact(contactId)?.Name ?? string.Empty;

    public string EntryCategoryName(string? id) =>
        (Book.FindEntryCategory(id) ?? Book.FindEntryCategory(EntryCategory.OtherId))?.Name(Language) ?? string.Empty;

    public string PeopleCategoryName(string? id) => Book.FindPeopleCategory(id)?.Name(Language) ?? string.Empty;

    public string Sym(Currency currency) => Fmt.Symbol(currency, Language);

    public string DateText(DateOnly date) => Fmt.Date(date, Language, Today);

    /// <summary>"Today", "Yesterday" or the date.</summary>
    public string DayLabel(DateOnly date)
    {
        if (date == Today)
        {
            return T("today");
        }

        return date == Today.AddDays(-1) ? T("yesterday") : DateText(date);
    }

    public static string NewId() => Guid.NewGuid().ToString("N");

    private void Rebuild()
    {
        Book = BookProjector.Project(events.Values);
    }

    private BookEvent? TryRead(string json)
    {
        try
        {
            return BookJsonContext.Deserialize(json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Skipped an unreadable event.");
            return null;
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "Skipped an event of an unknown kind.");
            return null;
        }
    }
}
