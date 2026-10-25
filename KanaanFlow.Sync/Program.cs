using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using KanaanFlow.Sync.Auth;
using KanaanFlow.Sync.Contracts;
using KanaanFlow.Sync.Data;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

NpgsqlConnectionStringBuilder connection = new NpgsqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("Kanaanflow") ?? throw new InvalidOperationException("Set ConnectionStrings__Kanaanflow."));

// The password comes separately so any characters in it are safe.
string? dbPassword = builder.Configuration["KF_DB_PASSWORD"];
if (!string.IsNullOrEmpty(dbPassword))
{
    connection.Password = dbPassword;
}

builder.Services.AddSingleton(NpgsqlDataSource.Create(connection.ConnectionString));
builder.Services.AddSingleton<SyncStore>();
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8 * 1024 * 1024);

// Slow down password guessing: a few sign-in attempts per minute per client.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("signin", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientIp(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Creating a new user needs this password. Set KF_MASTER_PASSWORD on the server to change it.
string masterPassword = builder.Configuration["KF_MASTER_PASSWORD"] is { Length: > 0 } configured ? configured : "WelcomeToKanaanFlow";

WebApplication app = builder.Build();
await app.Services.GetRequiredService<SyncStore>().MigrateAsync();
app.UseRateLimiter();

RouteGroupBuilder api = app.MapGroup("/api");

api.MapGet("/health", async (NpgsqlDataSource db) =>
{
    await using NpgsqlCommand command = db.CreateCommand("select 1");
    await command.ExecuteScalarAsync();
    return Results.Ok(new { status = "ok" });
});

api.MapGet("/status", async (SyncStore store) => Results.Ok(new StatusResponse(await store.IsReadyAsync())));

// Creating a new user needs the master password. New users are Admins.
api.MapPost("/register", async (RegisterRequest request, SyncStore store) =>
{
    if (!SameText(request.MasterPassword, masterPassword))
    {
        return Error("wrong-master-password", StatusCodes.Status403Forbidden);
    }

    string? problem = CheckName(request.Name) ?? CheckPassword(request.Password);
    if (problem != null)
    {
        return Error(problem);
    }

    SessionResponse? session = await store.RegisterAsync(request.Name, request.Password, request.MemberId);
    return session == null ? Error("name-taken", StatusCodes.Status409Conflict) : Results.Ok(session);
}).RequireRateLimiting("signin");

api.MapPost("/login", async (LoginRequest request, SyncStore store) =>
{
    SessionResponse? session = await store.LoginAsync(request.Name ?? string.Empty, request.Password ?? string.Empty);
    return session == null ? Error("wrong-name-or-password", StatusCodes.Status401Unauthorized) : Results.Ok(session);
}).RequireRateLimiting("signin");

api.MapPost("/logout", async (HttpContext context, SyncStore store) =>
{
    string? token = BearerToken(context);
    if (token != null)
    {
        await store.LogoutAsync(token);
    }

    return Results.NoContent();
});

api.MapGet("/me", async (HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    return me == null ? Unauthorized() : Results.Ok(me);
});

api.MapPost("/me/password", async (ChangePasswordRequest request, HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    if (me == null)
    {
        return Unauthorized();
    }

    string? problem = CheckPassword(request.New);
    if (problem != null)
    {
        return Error(problem);
    }

    bool changed = await store.ChangeOwnPasswordAsync(me.Id, request.Current ?? string.Empty, request.New, BearerToken(context)!);
    return changed ? Results.NoContent() : Error("wrong-password", StatusCodes.Status403Forbidden);
}).RequireRateLimiting("signin");

api.MapGet("/members", async (HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    return me == null ? Unauthorized() : Results.Ok(await store.MembersAsync());
});

api.MapPost("/members", async (CreateMemberRequest request, HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    if (me == null)
    {
        return Unauthorized();
    }

    string? problem = CheckName(request.Name) ?? CheckRole(request.Role) ?? CheckPassword(request.Password);
    if (problem != null)
    {
        return Error(problem);
    }

    string? denied = CheckAssign(me, request.Role);
    if (denied != null)
    {
        return Error(denied, StatusCodes.Status403Forbidden);
    }

    MemberDto? created = await store.CreateMemberAsync(request.Name, Enum.Parse<TeamRole>(request.Role), request.Password, me.Id);
    return created == null ? Error("name-taken", StatusCodes.Status409Conflict) : Results.Ok(created);
});

api.MapPatch("/members/{id}", async (string id, UpdateMemberRequest request, HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    if (me == null)
    {
        return Unauthorized();
    }

    string? problem = (request.Role != null ? CheckRole(request.Role) : null) ?? (request.Password != null ? CheckPassword(request.Password) : null);
    if (problem != null)
    {
        return Error(problem);
    }

    // Admins can always change anyone, passwords included. Owners and Accountants can't touch Admins.
    string? targetRole = await store.RoleOfAsync(id);
    if (targetRole == null)
    {
        return Error("not-found", StatusCodes.Status404NotFound);
    }

    string? denied = CheckEdit(me, targetRole) ?? (request.Role != null && request.Role != targetRole ? CheckAssign(me, request.Role) : null);
    if (denied != null)
    {
        return Error(denied, StatusCodes.Status403Forbidden);
    }

    string? failure = await store.UpdateMemberAsync(id, request, me.Id);
    return failure switch
    {
        null => Results.NoContent(),
        "not-found" => Error(failure, StatusCodes.Status404NotFound),
        _ => Error(failure, StatusCodes.Status409Conflict)
    };
});

// The admin panel: only Admins.
RouteGroupBuilder admin = api.MapGroup("/admin");

admin.MapGet("/overview", async (HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    return me == null ? Unauthorized() : !IsAdmin(me) ? AdminOnly() : Results.Ok(await store.OverviewAsync());
});

admin.MapGet("/sessions", async (HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    return me == null ? Unauthorized() : !IsAdmin(me) ? AdminOnly() : Results.Ok(await store.SessionsAsync());
});

admin.MapPost("/members/{id}/signout", async (string id, HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    if (me == null)
    {
        return Unauthorized();
    }

    if (!IsAdmin(me))
    {
        return AdminOnly();
    }

    return id == me.Id ? Error("cannot-remove-self", StatusCodes.Status409Conflict) : Results.Ok(new { ended = await store.SignOutEverywhereAsync(id) });
});

admin.MapGet("/activity", async (int? limit, HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    return me == null ? Unauthorized() : !IsAdmin(me) ? AdminOnly() : Results.Ok(await store.ActivityAsync(limit ?? 100));
});

// Download: everything after the last sequence number the device has.
api.MapGet("/events", async (long? after, int? limit, HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    if (me == null)
    {
        return Unauthorized();
    }

    List<(long Seq, string Payload)> rows = await store.EventsAfterAsync(Math.Max(0, after ?? 0), limit ?? SyncStore.MaxPage);
    JsonArray events = new JsonArray();
    foreach ((long seq, string payload) in rows)
    {
        events.Add(new JsonObject { ["seq"] = seq, ["event"] = JsonNode.Parse(payload) });
    }

    return Results.Content(new JsonObject { ["events"] = events }.ToJsonString(), "application/json");
});

// Upload: the device's new events. Each one is checked against the member's role.
api.MapPost("/events", async (HttpContext context, SyncStore store) =>
{
    MemberDto? me = await SignedIn(context, store);
    if (me == null)
    {
        return Unauthorized();
    }

    JsonArray? body;
    try
    {
        body = await JsonNode.ParseAsync(context.Request.Body) as JsonArray;
    }
    catch (JsonException)
    {
        body = null;
    }

    if (body == null || body.Count > SyncStore.MaxPage)
    {
        return Error("expected-array-of-events");
    }

    List<BookEvent> incoming = new List<BookEvent>();
    List<RejectedEvent> unreadable = new List<RejectedEvent>();
    foreach (JsonNode? node in body)
    {
        string id = node?["id"]?.GetValue<string>() ?? string.Empty;
        BookEvent? bookEvent = null;
        try
        {
            // An event without a change or a time would otherwise read as defaults.
            bool complete = node?["change"]?["kind"] != null && node["atUtc"] != null;
            bookEvent = complete ? BookJsonContext.Deserialize(node!.ToJsonString()) : null;
        }
        catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is InvalidOperationException)
        {
            bookEvent = null;
        }

        if (bookEvent == null || !SyncStore.IsValidId(bookEvent.Id))
        {
            unreadable.Add(new RejectedEvent(id, "unreadable"));
            continue;
        }

        incoming.Add(bookEvent);
    }

    PushResponse result = await store.PushAsync(me, incoming);
    result.Rejected.AddRange(unreadable);
    return Results.Ok(result);
});

app.Run();

static string ClientIp(HttpContext context)
{
    // nginx passes the visitor's address from Cloudflare; the API itself only listens on the Pi.
    string? forwarded = context.Request.Headers["X-Real-IP"].FirstOrDefault();
    return string.IsNullOrWhiteSpace(forwarded) ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown" : forwarded;
}

static string? BearerToken(HttpContext context)
{
    string header = context.Request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.Ordinal) && header.Length > 7 ? header.Substring(7).Trim() : null;
}

static async Task<MemberDto?> SignedIn(HttpContext context, SyncStore store)
{
    string? token = BearerToken(context);
    return token == null ? null : await store.AuthenticateAsync(token);
}

static string? CheckName(string? name)
{
    string trimmed = (name ?? string.Empty).Trim();
    return trimmed.Length is 0 or > 80 ? "invalid-name" : null;
}

static string? CheckPassword(string? password)
{
    return password == null || password.Length < PasswordHasher.MinLength || password.Length > 200 ? "password-too-short" : null;
}

static string? CheckRole(string? role)
{
    return Enum.TryParse(role, out TeamRole _) && Enum.IsDefined(Enum.Parse<TeamRole>(role!)) ? null : "invalid-role";
}

static TeamRole RoleOf(MemberDto member) => Enum.TryParse(member.Role, out TeamRole role) ? role : TeamRole.Viewer;

static bool IsAdmin(MemberDto member) => RoleOf(member) == TeamRole.Admin;

static string? CheckAssign(MemberDto me, string role)
{
    TeamRole actor = RoleOf(me);
    TeamRole wanted = Enum.Parse<TeamRole>(role);
    if (!RoleRules.CanManageMembers(actor))
    {
        return "owner-only";
    }

    if (!RoleRules.IsAvailable(wanted))
    {
        return "role-unavailable";
    }

    return RoleRules.CanAssign(actor, wanted) ? null : "cannot-manage-admin";
}

static string? CheckEdit(MemberDto me, string targetRole)
{
    TeamRole actor = RoleOf(me);
    if (!RoleRules.CanManageMembers(actor))
    {
        return "owner-only";
    }

    return RoleRules.CanEdit(actor, Enum.TryParse(targetRole, out TeamRole target) ? target : TeamRole.Viewer) ? null : "cannot-manage-admin";
}

static bool SameText(string? given, string expected)
{
    byte[] a = SHA256.HashData(Encoding.UTF8.GetBytes(given ?? string.Empty));
    byte[] b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
    return CryptographicOperations.FixedTimeEquals(a, b);
}

static IResult AdminOnly() => Error("admin-only", StatusCodes.Status403Forbidden);

static IResult Error(string code, int status = StatusCodes.Status400BadRequest) => Results.Json(new ErrorResponse(code), statusCode: status);

static IResult Unauthorized() => Error("signed-out", StatusCodes.Status401Unauthorized);

public partial class Program
{
}
