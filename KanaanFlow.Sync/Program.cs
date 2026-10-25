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

// First start: the first person to set up becomes the owner. Only works while the book has no members.
api.MapPost("/setup", async (SetupRequest request, SyncStore store) =>
{
    string? problem = CheckName(request.Name) ?? CheckPassword(request.Password);
    if (problem != null)
    {
        return Error(problem);
    }

    SessionResponse? session = await store.SetupAsync(request.Name, request.Password, request.MemberId);
    return session == null ? Error("already-set-up", StatusCodes.Status409Conflict) : Results.Ok(session);
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

    if (me.Role != nameof(TeamRole.Owner))
    {
        return Error("owner-only", StatusCodes.Status403Forbidden);
    }

    string? problem = CheckName(request.Name) ?? CheckRole(request.Role) ?? CheckPassword(request.Password);
    if (problem != null)
    {
        return Error(problem);
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

    if (me.Role != nameof(TeamRole.Owner))
    {
        return Error("owner-only", StatusCodes.Status403Forbidden);
    }

    string? problem = (request.Role != null ? CheckRole(request.Role) : null) ?? (request.Password != null ? CheckPassword(request.Password) : null);
    if (problem != null)
    {
        return Error(problem);
    }

    string? failure = await store.UpdateMemberAsync(id, request, me.Id);
    return failure switch
    {
        null => Results.NoContent(),
        "not-found" => Error(failure, StatusCodes.Status404NotFound),
        _ => Error(failure, StatusCodes.Status409Conflict)
    };
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

static IResult Error(string code, int status = StatusCodes.Status400BadRequest) => Results.Json(new ErrorResponse(code), statusCode: status);

static IResult Unauthorized() => Error("signed-out", StatusCodes.Status401Unauthorized);

public partial class Program
{
}
