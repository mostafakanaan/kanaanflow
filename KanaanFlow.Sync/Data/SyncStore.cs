namespace KanaanFlow.Sync.Data;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using KanaanFlow.Sync.Auth;
using KanaanFlow.Sync.Contracts;
using KanaanFlow.Sync.Policy;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

/// <summary>
/// Members, sessions and the append-only event log in Postgres.
/// </summary>
public sealed class SyncStore
{
    public const int MaxPage = 500;

    // Writers to the log take this lock so sequence numbers are handed out in commit order:
    // a device that has read up to seq N never misses an event that commits later with a smaller seq.
    private const long LogLock = 0x4B464C4F47; // "KFLOG"

    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(180);

    private readonly NpgsqlDataSource db;
    private readonly ILogger<SyncStore> logger;

    public SyncStore(NpgsqlDataSource db, ILogger<SyncStore> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    /// <summary>Creates or updates the tables, retrying while the database starts up.</summary>
    public async Task MigrateAsync()
    {
        string sql;
        using (Stream stream = typeof(SyncStore).Assembly.GetManifestResourceStream("KanaanFlow.Sync.Schema.sql")!)
        using (StreamReader reader = new StreamReader(stream))
        {
            sql = await reader.ReadToEndAsync();
        }

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using NpgsqlCommand command = db.CreateCommand(sql);
                await command.ExecuteNonQueryAsync();
                break;
            }
            catch (NpgsqlException ex) when (attempt < 30)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}); retrying.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        await PromoteFirstOwnerAsync();
    }

    /// <summary>
    /// Books created before the Admin role existed: the first owner becomes the Admin, so someone can still do everything.
    /// </summary>
    public async Task PromoteFirstOwnerAsync()
    {
        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        await LockLogAsync(connection, tx);
        if (await CountActiveAdminsAsync(connection, tx) > 0)
        {
            return;
        }

        MemberDto? owner = null;
        await using (NpgsqlCommand find = new NpgsqlCommand(
            "select id, name from members where active and role = 'Owner' order by created_at limit 1", connection, tx))
        await using (NpgsqlDataReader reader = await find.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                owner = new MemberDto(reader.GetString(0), reader.GetString(1), nameof(TeamRole.Admin), true);
            }
        }

        if (owner == null)
        {
            return;
        }

        await using (NpgsqlCommand update = new NpgsqlCommand("update members set role = 'Admin' where id = @id", connection, tx))
        {
            update.Parameters.AddWithValue("id", owner.Id);
            await update.ExecuteNonQueryAsync();
        }

        await AppendMemberEventAsync(connection, tx, owner, owner.Id);
        await tx.CommitAsync();
        logger.LogInformation("Made {Name} the Admin of this book.", owner.Name);
    }

    public async Task<bool> IsReadyAsync()
    {
        await using NpgsqlCommand command = db.CreateCommand("select exists (select 1 from members)");
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Creates a new Admin (the person knew the master password). Keeps the device's member id when it is free,
    /// so entries made on that device before registering stay theirs. Returns null when an active member has that name.
    /// </summary>
    public async Task<SessionResponse?> RegisterAsync(string name, string password, string? memberId)
    {
        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        await LockLogAsync(connection, tx);

        if (await NameTakenAsync(connection, tx, name, exceptId: null))
        {
            return null;
        }

        string id = IsValidId(memberId) && !await MemberExistsAsync(connection, tx, memberId!) ? memberId! : NewId();
        MemberDto admin = new MemberDto(id, name.Trim(), nameof(TeamRole.Admin), true);
        await InsertMemberAsync(connection, tx, admin, PasswordHasher.Hash(password));
        await AppendMemberEventAsync(connection, tx, admin, actorId: id);
        string token = await CreateSessionAsync(connection, tx, id);
        await tx.CommitAsync();
        return new SessionResponse(token, admin);
    }

    public async Task<SessionResponse?> LoginAsync(string name, string password)
    {
        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        string? id = null;
        string? hash = null;
        MemberDto? member = null;
        await using (NpgsqlCommand find = new NpgsqlCommand(
            "select id, name, role, password_hash from members where active and lower(name) = lower(@name)", connection))
        {
            find.Parameters.AddWithValue("name", name.Trim());
            await using NpgsqlDataReader reader = await find.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                id = reader.GetString(0);
                member = new MemberDto(id, reader.GetString(1), reader.GetString(2), true);
                hash = reader.GetString(3);
            }
        }

        if (member == null || hash == null || !PasswordHasher.Verify(password, hash))
        {
            // Spend the same time when the name is unknown, so names can't be probed.
            PasswordHasher.Verify(password, "pbkdf2-sha256$150000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
            return null;
        }

        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        string token = await CreateSessionAsync(connection, tx, id!);
        await tx.CommitAsync();
        return new SessionResponse(token, member);
    }

    /// <summary>
    /// Sets a new password for the active member with this name (the person knew the master password), signs them out
    /// everywhere and returns a new session. Returns null when no active member has that name.
    /// </summary>
    public async Task<SessionResponse?> ResetPasswordByNameAsync(string name, string newPassword, string clientIp)
    {
        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        MemberDto? member = null;
        await using (NpgsqlCommand find = new NpgsqlCommand(
            "select id, name, role from members where active and lower(name) = lower(@name)", connection, tx))
        {
            find.Parameters.AddWithValue("name", name.Trim());
            await using NpgsqlDataReader reader = await find.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                member = new MemberDto(reader.GetString(0), reader.GetString(1), reader.GetString(2), true);
            }
        }

        // Hash either way, so an unknown name takes as long as a real one and names can't be probed.
        string hash = PasswordHasher.Hash(newPassword);
        if (member == null)
        {
            return null;
        }

        await using (NpgsqlCommand update = new NpgsqlCommand("update members set password_hash = @hash where id = @id", connection, tx))
        {
            update.Parameters.AddWithValue("hash", hash);
            update.Parameters.AddWithValue("id", member.Id);
            await update.ExecuteNonQueryAsync();
        }

        await using (NpgsqlCommand signOut = new NpgsqlCommand("delete from sessions where member_id = @id", connection, tx))
        {
            signOut.Parameters.AddWithValue("id", member.Id);
            await signOut.ExecuteNonQueryAsync();
        }

        string token = await CreateSessionAsync(connection, tx, member.Id);
        await tx.CommitAsync();
        logger.LogWarning("Password of {Name} ({Id}) was reset with the master password from {Ip}.", member.Name, member.Id, clientIp);
        return new SessionResponse(token, member);
    }

    /// <summary>The active member a session token belongs to, extending the session as it is used.</summary>
    public async Task<MemberDto?> AuthenticateAsync(string token)
    {
        await using NpgsqlCommand command = db.CreateCommand(
            "update sessions s set last_seen_at = now(), expires_at = now() + @lifetime "
            + "from members m where s.token_hash = @hash and s.expires_at > now() and m.id = s.member_id and m.active "
            + "returning m.id, m.name, m.role");
        command.Parameters.AddWithValue("hash", PasswordHasher.HashToken(token));
        command.Parameters.AddWithValue("lifetime", SessionLifetime);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new MemberDto(reader.GetString(0), reader.GetString(1), reader.GetString(2), true) : null;
    }

    public async Task LogoutAsync(string token)
    {
        await using NpgsqlCommand command = db.CreateCommand("delete from sessions where token_hash = @hash");
        command.Parameters.AddWithValue("hash", PasswordHasher.HashToken(token));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The role of a member, or null when there is no such member.</summary>
    public async Task<string?> RoleOfAsync(string id)
    {
        await using NpgsqlCommand command = db.CreateCommand("select role from members where id = @id");
        command.Parameters.AddWithValue("id", id);
        return (string?)await command.ExecuteScalarAsync();
    }

    public async Task<List<MemberDto>> MembersAsync()
    {
        List<MemberDto> members = new List<MemberDto>();
        await using NpgsqlCommand command = db.CreateCommand("select id, name, role, active from members order by active desc, created_at");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            members.Add(new MemberDto(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        }

        return members;
    }

    /// <summary>Adds a member. Returns null when an active member already has that name.</summary>
    public async Task<MemberDto?> CreateMemberAsync(string name, TeamRole role, string password, string actorId)
    {
        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        await LockLogAsync(connection, tx);
        if (await NameTakenAsync(connection, tx, name, exceptId: null))
        {
            return null;
        }

        MemberDto member = new MemberDto(NewId(), name.Trim(), role.ToString(), true);
        await InsertMemberAsync(connection, tx, member, PasswordHasher.Hash(password));
        await AppendMemberEventAsync(connection, tx, member, actorId);
        await tx.CommitAsync();
        return member;
    }

    /// <summary>Changes a member's role, password or active state. Returns an error code, or null on success.</summary>
    public async Task<string?> UpdateMemberAsync(string id, UpdateMemberRequest request, string actorId)
    {
        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        await LockLogAsync(connection, tx);

        MemberDto? current = null;
        await using (NpgsqlCommand find = new NpgsqlCommand("select id, name, role, active from members where id = @id", connection, tx))
        {
            find.Parameters.AddWithValue("id", id);
            await using NpgsqlDataReader reader = await find.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                current = new MemberDto(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3));
            }
        }

        if (current == null)
        {
            return "not-found";
        }

        string role = request.Role ?? current.Role;
        bool active = request.Active ?? current.Active;
        bool losesAdmin = current.Role == nameof(TeamRole.Admin) && current.Active && (role != nameof(TeamRole.Admin) || !active);
        if (losesAdmin && await CountActiveAdminsAsync(connection, tx) <= 1)
        {
            return "last-admin";
        }

        if (!active && id == actorId)
        {
            return "cannot-remove-self";
        }

        if (active && !current.Active && await NameTakenAsync(connection, tx, current.Name, exceptId: id))
        {
            return "name-taken";
        }

        await using (NpgsqlCommand update = new NpgsqlCommand(
            "update members set role = @role, active = @active, password_hash = coalesce(@hash, password_hash) where id = @id", connection, tx))
        {
            update.Parameters.AddWithValue("role", role);
            update.Parameters.AddWithValue("active", active);
            update.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Text) { Value = request.Password != null ? PasswordHasher.Hash(request.Password) : DBNull.Value });
            update.Parameters.AddWithValue("id", id);
            await update.ExecuteNonQueryAsync();
        }

        if (!active || request.Password != null)
        {
            // Removed members, and anyone whose password was reset, sign in again everywhere.
            await using NpgsqlCommand signOut = new NpgsqlCommand("delete from sessions where member_id = @id", connection, tx);
            signOut.Parameters.AddWithValue("id", id);
            await signOut.ExecuteNonQueryAsync();
        }

        if (!active && current.Active)
        {
            await AppendAsync(connection, tx, new MemberRemoved { MemberId = id }, actorId);
        }
        else if (active && (role != current.Role || !current.Active))
        {
            await AppendMemberEventAsync(connection, tx, current with { Role = role, Active = true }, actorId);
        }

        await tx.CommitAsync();
        return null;
    }

    public async Task<bool> ChangeOwnPasswordAsync(string id, string currentPassword, string newPassword, string keepToken)
    {
        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        string? hash;
        await using (NpgsqlCommand find = new NpgsqlCommand("select password_hash from members where id = @id and active", connection))
        {
            find.Parameters.AddWithValue("id", id);
            hash = (string?)await find.ExecuteScalarAsync();
        }

        if (hash == null || !PasswordHasher.Verify(currentPassword, hash))
        {
            return false;
        }

        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        await using (NpgsqlCommand update = new NpgsqlCommand("update members set password_hash = @hash where id = @id", connection, tx))
        {
            update.Parameters.AddWithValue("hash", PasswordHasher.Hash(newPassword));
            update.Parameters.AddWithValue("id", id);
            await update.ExecuteNonQueryAsync();
        }

        // Sign out the member's other devices, keep this one.
        await using (NpgsqlCommand signOut = new NpgsqlCommand("delete from sessions where member_id = @id and token_hash <> @keep", connection, tx))
        {
            signOut.Parameters.AddWithValue("id", id);
            signOut.Parameters.AddWithValue("keep", PasswordHasher.HashToken(keepToken));
            await signOut.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return true;
    }

    /// <summary>Events after <paramref name="after"/>, as stored JSON, in server order.</summary>
    public async Task<List<(long Seq, string Payload)>> EventsAfterAsync(long after, int limit)
    {
        List<(long, string)> rows = new List<(long, string)>();
        await using NpgsqlCommand command = db.CreateCommand("select seq, payload::text from events where seq > @after order by seq limit @limit");
        command.Parameters.AddWithValue("after", after);
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, MaxPage));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt64(0), reader.GetString(1)));
        }

        return rows;
    }

    /// <summary>Checks each event against the member's role and appends the allowed ones. Re-sent events count as accepted.</summary>
    public async Task<PushResponse> PushAsync(MemberDto member, IReadOnlyList<BookEvent> incoming)
    {
        TeamRole role = Enum.Parse<TeamRole>(member.Role);
        List<string> accepted = new List<string>();
        List<RejectedEvent> rejected = new List<RejectedEvent>();

        await using NpgsqlConnection connection = await db.OpenConnectionAsync();
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync();
        await LockLogAsync(connection, tx);

        HashSet<string> known = await ExistingIdsAsync(connection, tx, incoming.Select(e => e.Id).ToList());
        HashSet<string> purged = await PurgedAmongAsync(
            connection, tx, incoming.Select(e => EntryChanges.EntryIdOf(e.Change)).OfType<string>().Distinct().ToList());
        foreach (BookEvent bookEvent in incoming)
        {
            if (known.Contains(bookEvent.Id))
            {
                accepted.Add(bookEvent.Id);
                continue;
            }

            // Every change is tied to the person who made it, so nobody can record in someone else's name.
            string? entryId = EntryChanges.EntryIdOf(bookEvent.Change);
            string? reason = bookEvent.AtUtc > DateTime.UtcNow.AddDays(1)
                ? "time-in-future"
                : bookEvent.Actor != member.Id
                ? "wrong-actor"
                : entryId != null && purged.Contains(entryId)
                ? "entry-purged"
                : (bookEvent.Change is EntryDeleted || bookEvent.Change is EntryPurged) && await IsLoanPrincipalAsync(connection, tx, entryId!)
                ? "cannot-delete-loan-entry"
                : ChangePolicy.Check(
                    role,
                    member.Id,
                    bookEvent.Change,
                    entryId => OwnerOf(connection, tx, "entry.recorded", "entry", entryId),
                    loanId => OwnerOf(connection, tx, "loan.opened", "loan", loanId));

            if (reason != null)
            {
                rejected.Add(new RejectedEvent(bookEvent.Id, reason));
                continue;
            }

            await InsertEventAsync(connection, tx, bookEvent, member.Id);
            known.Add(bookEvent.Id);
            accepted.Add(bookEvent.Id);
            if (bookEvent.Change is EntryPurged purge)
            {
                purged.Add(purge.EntryId);
                int removed = await PurgeEntryAsync(connection, tx, purge.EntryId);
                logger.LogWarning("{Name} permanently deleted entry {EntryId} ({Removed} events removed).", member.Name, purge.EntryId, removed);
            }
        }

        await tx.CommitAsync();
        return new PushResponse(accepted, rejected);
    }

    /// <summary>Counts for the admin panel.</summary>
    public async Task<AdminOverview> OverviewAsync()
    {
        await using NpgsqlCommand command = db.CreateCommand(
            "select (select count(*) from members where active), (select count(*) from events), "
            + "(select count(*) from sessions where expires_at > now()), (select max(received_at) from events)");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new AdminOverview(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.IsDBNull(3) ? null : DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc));
    }

    /// <summary>Signed-in devices, most recently used first.</summary>
    public async Task<List<AdminSession>> SessionsAsync()
    {
        List<AdminSession> sessions = new List<AdminSession>();
        await using NpgsqlCommand command = db.CreateCommand(
            "select m.id, m.name, s.created_at, s.last_seen_at from sessions s join members m on m.id = s.member_id "
            + "where s.expires_at > now() and m.active order by s.last_seen_at desc limit 200");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new AdminSession(
                reader.GetString(0),
                reader.GetString(1),
                DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc),
                DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)));
        }

        return sessions;
    }

    /// <summary>Signs a member out on every device. Returns how many sessions ended.</summary>
    public async Task<int> SignOutEverywhereAsync(string memberId)
    {
        await using NpgsqlCommand command = db.CreateCommand("delete from sessions where member_id = @id");
        command.Parameters.AddWithValue("id", memberId);
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>The newest changes in the log with who made them: the book's audit trail.</summary>
    public async Task<List<AdminActivity>> ActivityAsync(int limit)
    {
        List<AdminActivity> rows = new List<AdminActivity>();
        await using NpgsqlCommand command = db.CreateCommand(
            "select e.seq, e.at, e.received_at, e.kind, e.device_id, m.name from events e join members m on m.id = e.member_id "
            + "order by e.seq desc limit @limit");
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, MaxPage));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new AdminActivity(
                reader.GetInt64(0),
                DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return rows;
    }

    public static bool IsValidId(string? id) => id != null && id.Length is > 0 and <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static async Task LockLogAsync(NpgsqlConnection connection, NpgsqlTransaction tx)
    {
        await using NpgsqlCommand command = new NpgsqlCommand("select pg_advisory_xact_lock(@key)", connection, tx);
        command.Parameters.AddWithValue("key", LogLock);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> CreateSessionAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string memberId)
    {
        (string token, string tokenHash) = PasswordHasher.NewToken();
        await using NpgsqlCommand command = new NpgsqlCommand(
            "insert into sessions (token_hash, member_id, expires_at) values (@hash, @member, now() + @lifetime)", connection, tx);
        command.Parameters.AddWithValue("hash", tokenHash);
        command.Parameters.AddWithValue("member", memberId);
        command.Parameters.AddWithValue("lifetime", SessionLifetime);
        await command.ExecuteNonQueryAsync();
        return token;
    }

    private static async Task InsertMemberAsync(NpgsqlConnection connection, NpgsqlTransaction tx, MemberDto member, string passwordHash)
    {
        await using NpgsqlCommand command = new NpgsqlCommand(
            "insert into members (id, name, role, password_hash) values (@id, @name, @role, @hash)", connection, tx);
        command.Parameters.AddWithValue("id", member.Id);
        command.Parameters.AddWithValue("name", member.Name);
        command.Parameters.AddWithValue("role", member.Role);
        command.Parameters.AddWithValue("hash", passwordHash);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> NameTakenAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string name, string? exceptId)
    {
        await using NpgsqlCommand command = new NpgsqlCommand(
            "select exists (select 1 from members where active and lower(name) = lower(@name) and id <> coalesce(@except, ''))", connection, tx);
        command.Parameters.AddWithValue("name", name.Trim());
        command.Parameters.Add(new NpgsqlParameter("except", NpgsqlDbType.Text) { Value = (object?)exceptId ?? DBNull.Value });
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> CountActiveAdminsAsync(NpgsqlConnection connection, NpgsqlTransaction tx)
    {
        await using NpgsqlCommand command = new NpgsqlCommand("select count(*) from members where active and role = 'Admin'", connection, tx);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> MemberExistsAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string id)
    {
        await using NpgsqlCommand command = new NpgsqlCommand("select exists (select 1 from members where id = @id)", connection, tx);
        command.Parameters.AddWithValue("id", id);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Tells every device about a member, so the team list and roles show in the app.</summary>
    private static Task AppendMemberEventAsync(NpgsqlConnection connection, NpgsqlTransaction tx, MemberDto member, string actorId)
    {
        TeamMember teamMember = new TeamMember { Id = member.Id, Name = member.Name, Role = Enum.Parse<TeamRole>(member.Role) };
        return AppendAsync(connection, tx, new MemberSaved { Member = teamMember }, actorId);
    }

    private static Task AppendAsync(NpgsqlConnection connection, NpgsqlTransaction tx, BookChange change, string actorId)
    {
        BookEvent bookEvent = new BookEvent { Id = NewId(), AtUtc = DateTime.UtcNow, DeviceId = "server", Actor = actorId, Change = change };
        return InsertEventAsync(connection, tx, bookEvent, actorId);
    }

    private static async Task InsertEventAsync(NpgsqlConnection connection, NpgsqlTransaction tx, BookEvent bookEvent, string memberId)
    {
        string payload = BookJsonContext.Serialize(bookEvent);
        string kind;
        using (JsonDocument doc = JsonDocument.Parse(payload))
        {
            kind = doc.RootElement.GetProperty("change").GetProperty("kind").GetString() ?? "unknown";
        }

        await using NpgsqlCommand command = new NpgsqlCommand(
            "insert into events (id, member_id, device_id, at, kind, payload) values (@id, @member, @device, @at, @kind, @payload) on conflict (id) do nothing",
            connection,
            tx);
        command.Parameters.AddWithValue("id", bookEvent.Id);
        command.Parameters.AddWithValue("member", memberId);
        command.Parameters.AddWithValue("device", bookEvent.DeviceId.Length > 64 ? bookEvent.DeviceId.Substring(0, 64) : bookEvent.DeviceId);
        command.Parameters.AddWithValue("at", DateTime.SpecifyKind(bookEvent.AtUtc, DateTimeKind.Utc));
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = payload });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> ExistingIdsAsync(NpgsqlConnection connection, NpgsqlTransaction tx, List<string> ids)
    {
        HashSet<string> found = new HashSet<string>();
        await using NpgsqlCommand command = new NpgsqlCommand("select id from events where id = any(@ids)", connection, tx);
        command.Parameters.AddWithValue("ids", ids.ToArray());
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found.Add(reader.GetString(0));
        }

        return found;
    }

    /// <summary>Which of these entries were deleted for good.</summary>
    private static async Task<HashSet<string>> PurgedAmongAsync(NpgsqlConnection connection, NpgsqlTransaction tx, List<string> entryIds)
    {
        HashSet<string> found = new HashSet<string>();
        await using NpgsqlCommand command = new NpgsqlCommand(
            "select payload -> 'change' ->> 'entryId' from events where kind = 'entry.purged' and payload -> 'change' ->> 'entryId' = any(@ids)", connection, tx);
        command.Parameters.AddWithValue("ids", entryIds.ToArray());
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found.Add(reader.GetString(0));
        }

        return found;
    }

    private static async Task<bool> IsLoanPrincipalAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string entryId)
    {
        await using NpgsqlCommand command = new NpgsqlCommand(
            "select exists (select 1 from events where kind = 'entry.recorded' and payload -> 'change' -> 'entry' ->> 'id' = @id "
            + "and (payload -> 'change' -> 'entry' ->> 'isLoanPrincipal')::boolean)", connection, tx);
        command.Parameters.AddWithValue("id", entryId);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Removes an entry and every change about it from the log, keeping only the purge tombstone.</summary>
    private static async Task<int> PurgeEntryAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string entryId)
    {
        await using NpgsqlCommand command = new NpgsqlCommand(
            "delete from events where (kind = 'entry.recorded' and payload -> 'change' -> 'entry' ->> 'id' = @id) "
            + "or (kind <> 'entry.purged' and payload -> 'change' ->> 'entryId' = @id)", connection, tx);
        command.Parameters.AddWithValue("id", entryId);
        return await command.ExecuteNonQueryAsync();
    }

    private static string? OwnerOf(NpgsqlConnection connection, NpgsqlTransaction tx, string kind, string property, string id)
    {
        // Runs inside the push transaction, so entries uploaded earlier in the same batch are found too.
        using NpgsqlCommand command = new NpgsqlCommand(
            $"select member_id from events where kind = @kind and payload -> 'change' -> '{property}' ->> 'id' = @id limit 1", connection, tx);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("id", id);
        return (string?)command.ExecuteScalar();
    }
}
