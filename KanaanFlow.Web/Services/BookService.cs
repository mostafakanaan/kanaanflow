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

    /// <summary>The app is only usable while someone is logged in: signed in to the team server, or on a device-only book.</summary>
    public bool IsLoggedIn => Settings.SetupDone && Settings.MemberId.Length > 0 && (Settings.SignedIn || Settings.LocalOnly);

    public TeamMember? Me => Book.FindMember(Settings.MemberId);

    /// <summary>The logged-in member's role: from the synced team list, or what the server said at sign-in.</summary>
    public TeamRole MyRole => Me?.Role ?? Settings.Role;

    public string MyName => Me?.Name ?? Settings.LocalAccounts.FirstOrDefault(a => a.MemberId == Settings.MemberId)?.Name ?? string.Empty;

    public bool CanRecord => IsLoggedIn && RoleRules.CanRecord(MyRole);

    public bool CanManageBook => IsLoggedIn && RoleRules.CanManageBook(MyRole);

    public bool CanManageMembers => IsLoggedIn && RoleRules.CanManageMembers(MyRole);

    public bool CanApprove => IsLoggedIn && RoleRules.CanApprove(MyRole);

    public bool IsAdmin => IsLoggedIn && MyRole == TeamRole.Admin;

    public bool CanDelete => IsLoggedIn && RoleRules.CanDelete(MyRole);

    public bool CanPurge => IsLoggedIn && RoleRules.CanPurge(MyRole);

    /// <summary>Name of the team member with this id ("—" when unknown).</summary>
    public string MemberName(string? id)
    {
        TeamMember? member = Book.FindMember(id);
        return member != null && member.Name.Length > 0 ? member.Name : "—";
    }

    public string T(string key)
    {
        Dictionary<string, string> table = Language == "en" ? Strings.En : Strings.Ar;
        return table.TryGetValue(key, out string? text) ? text : key;
    }

    /// <summary>Text with a number in place of "{0}".</summary>
    public string T(string key, int number) => T(key).Replace("{0}", number.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
        await UpgradeSettingsAsync();
        _ = store.RequestPersistenceAsync().AsTask();
    }

    /// <summary>Remembers who is logged in on this device.</summary>
    public async Task LogInAsync(string memberId, TeamRole role, bool localOnly)
    {
        Settings.MemberId = memberId;
        Settings.Role = role;
        Settings.LocalOnly = localOnly;
        Settings.SetupDone = true;
        await SaveSettingsAsync();
    }

    /// <summary>Forgets who is logged in. The book stays on the device; unsynced changes wait for their author.</summary>
    public async Task LogOutAsync()
    {
        Settings.Token = string.Empty;
        Settings.MemberId = string.Empty;
        Settings.Role = TeamRole.Viewer;
        Settings.SetupDone = false;
        await SaveSettingsAsync();
    }

    /// <summary>Unsynced changes made by the logged-in member.</summary>
    public IReadOnlyList<BookEvent> MyUnsyncedEvents()
    {
        return UnsyncedEvents().Where(e => e.Actor == Settings.MemberId).ToList();
    }

    /// <summary>
    /// Unsynced changes made by someone the team server doesn't know (a device-only user, before signing in to the team).
    /// Changes by other team members are left alone: they upload when that person logs in again.
    /// </summary>
    public IReadOnlyList<BookEvent> ForeignUnsyncedEvents()
    {
        HashSet<string> teamMembers = events.Values
            .Where(e => !unsynced.Contains(e.Id) && e.Change is MemberSaved)
            .Select(e => ((MemberSaved)e.Change).Member.Id)
            .ToHashSet();

        return UnsyncedEvents()
            .Where(e => e.Change is not MemberSaved and not MemberRemoved && e.Actor != Settings.MemberId && !teamMembers.Contains(e.Actor))
            .ToList();
    }

    /// <summary>Makes unsynced changes the logged-in member's own, so they can upload under their name.</summary>
    public async Task ClaimUnsyncedAsync(IReadOnlyList<string> ids)
    {
        List<StoredEvent> rows = new List<StoredEvent>();
        foreach (string id in ids.Where(unsynced.Contains))
        {
            BookEvent bookEvent = events[id];
            bookEvent.Actor = Settings.MemberId;
            if (bookEvent.Change is EntryRecorded recorded)
            {
                recorded.Entry.CreatedBy = Settings.MemberId;
            }

            rows.Add(new StoredEvent { Id = id, Json = BookJsonContext.Serialize(bookEvent), Synced = false });
        }

        if (rows.Count > 0)
        {
            await store.PutEventsAsync(rows);
            Rebuild();
            Changed?.Invoke();
        }
    }

    private async Task UpgradeSettingsAsync()
    {
        if (Settings.Version >= 1)
        {
            return;
        }

        // Before log-in existed: a device that was set up stays logged in. Without a server it is a device-only
        // book whose user becomes its Admin; the first password they log in with becomes theirs.
        if (Settings.SetupDone && Settings.MemberId.Length > 0)
        {
            if (Settings.SignedIn)
            {
                Settings.Role = Me?.Role ?? TeamRole.Viewer;
            }
            else
            {
                Settings.LocalOnly = true;
                Settings.Role = TeamRole.Admin;
                Settings.LocalAccounts.Add(new LocalAccount { MemberId = Settings.MemberId, Name = Me?.Name ?? string.Empty });
                if (Me != null && Me.Role != TeamRole.Admin)
                {
                    TeamMember admin = Me.Clone();
                    admin.Role = TeamRole.Admin;
                    await RecordAsync(new MemberSaved { Member = admin });
                }
            }
        }
        else
        {
            Settings.SetupDone = false;
        }

        Settings.Version = 1;
        await SaveSettingsAsync();
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
        await RemovePurgedAsync(changes);
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
            await RemovePurgedAsync(incoming.Select(e => e.Change));
            Rebuild();
            Changed?.Invoke();
        }

        return rows.Count;
    }

    /// <summary>The whole event log as a JSON array, oldest first.</summary>
    public string ExportEventsJson()
    {
        return "[" + string.Join(",", events.Values.OrderBy(e => e.AtUtc).ThenBy(e => e.Id, StringComparer.Ordinal).Select(BookJsonContext.Serialize)) + "]";
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

    /// <summary>
    /// An entry deleted for good: removes it and every change about it from this device, keeping only the purge itself
    /// (the tombstone that tells other devices to do the same).
    /// </summary>
    private async Task RemovePurgedAsync(IEnumerable<BookChange> changes)
    {
        HashSet<string> purged = changes.OfType<EntryPurged>().Select(p => p.EntryId).ToHashSet();
        if (purged.Count == 0)
        {
            return;
        }

        List<string> drop = events.Values.Where(e => e.Change switch
        {
            EntryRecorded recorded => purged.Contains(recorded.Entry.Id),
            EntryVoided voided => purged.Contains(voided.EntryId),
            EntryApproved approved => purged.Contains(approved.EntryId),
            EntryDeleted deleted => purged.Contains(deleted.EntryId),
            EntryRestored restored => purged.Contains(restored.EntryId),
            _ => false
        }).Select(e => e.Id).ToList();

        if (drop.Count > 0)
        {
            await store.DeleteEventsAsync(drop);
            foreach (string id in drop)
            {
                events.Remove(id);
                unsynced.Remove(id);
            }
        }
    }

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
