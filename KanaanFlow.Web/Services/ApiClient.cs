namespace KanaanFlow.Web.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

public sealed record ApiMember(string Id, string Name, TeamRole Role, bool Active);

/// <summary>Epoch: the version of the book on the server, new after every reset.</summary>
public sealed record ApiSession(string Token, ApiMember Member, string Epoch);

public sealed record ApiOverview(long Members, long Events, long Sessions, DateTime? LastChangeUtc);

public sealed record ApiDeviceSession(string MemberId, string Name, DateTime CreatedUtc, DateTime LastSeenUtc);

public sealed record ApiActivity(long Seq, DateTime AtUtc, string Kind, string DeviceId, string Name);

public sealed record PushResult(List<string> Accepted, List<(string Id, string Reason)> Rejected);

/// <summary>
/// Talks to the KanaanFlow sync server, which is served from the same address as the app (under /api).
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient http;
    private readonly string baseUri;
    private readonly BookService book;

    public ApiClient(HttpClient http, IWebAssemblyHostEnvironment host, BookService book)
    {
        this.http = http;
        baseUri = host.BaseAddress;
        this.book = book;
    }

    /// <summary>True when the server's book already has an owner; null when there is no sync server at this address.</summary>
    public async Task<bool?> IsReadyAsync()
    {
        try
        {
            JsonNode? body = await SendAsync(HttpMethod.Get, "status", null, authorized: false);
            return body?["ready"]?.GetValue<bool>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (ApiException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Creates a new Admin on the server; needs the master password.</summary>
    public async Task<ApiSession> RegisterAsync(string name, string password, string masterPassword, string memberId)
    {
        JsonObject request = new JsonObject { ["name"] = name, ["password"] = password, ["masterPassword"] = masterPassword, ["memberId"] = memberId };
        JsonNode? body = await SendAsync(HttpMethod.Post, "register", request, authorized: false);
        return ReadSession(body);
    }

    public async Task<ApiSession> LoginAsync(string name, string password)
    {
        JsonNode? body = await SendAsync(HttpMethod.Post, "login", new JsonObject { ["name"] = name, ["password"] = password }, authorized: false);
        return ReadSession(body);
    }

    /// <summary>Sets a new password for a forgotten one (needs the master password) and signs in.</summary>
    public async Task<ApiSession> ResetPasswordAsync(string name, string masterPassword, string newPassword)
    {
        JsonObject request = new JsonObject { ["name"] = name, ["masterPassword"] = masterPassword, ["newPassword"] = newPassword };
        JsonNode? body = await SendAsync(HttpMethod.Post, "reset-password", request, authorized: false);
        return ReadSession(body);
    }

    public Task LogoutAsync() => SendAsync(HttpMethod.Post, "logout", null, authorized: true);

    public Task ChangePasswordAsync(string current, string next) =>
        SendAsync(HttpMethod.Post, "me/password", new JsonObject { ["current"] = current, ["new"] = next }, authorized: true);

    public async Task<ApiMember> CreateMemberAsync(string name, TeamRole role, string password)
    {
        JsonNode? body = await SendAsync(HttpMethod.Post, "members", new JsonObject { ["name"] = name, ["role"] = role.ToString(), ["password"] = password }, authorized: true);
        return ReadMember(body);
    }

    public async Task<List<ApiMember>> MembersAsync()
    {
        JsonNode? body = await SendAsync(HttpMethod.Get, "members", null, authorized: true);
        List<ApiMember> members = new List<ApiMember>();
        foreach (JsonNode? node in body?.AsArray() ?? new JsonArray())
        {
            members.Add(ReadMember(node));
        }

        return members;
    }

    public Task UpdateMemberAsync(string id, TeamRole? role = null, string? password = null, bool? active = null)
    {
        JsonObject request = new JsonObject();
        if (role != null)
        {
            request["role"] = role.ToString();
        }

        if (password != null)
        {
            request["password"] = password;
        }

        if (active != null)
        {
            request["active"] = active;
        }

        return SendAsync(HttpMethod.Patch, "members/" + id, request, authorized: true);
    }

    public async Task<ApiOverview> AdminOverviewAsync()
    {
        JsonNode? body = await SendAsync(HttpMethod.Get, "admin/overview", null, authorized: true);
        return new ApiOverview(
            body!["members"]!.GetValue<long>(),
            body["events"]!.GetValue<long>(),
            body["sessions"]!.GetValue<long>(),
            ReadTime(body["lastChangeUtc"]));
    }

    public async Task<List<ApiDeviceSession>> AdminSessionsAsync()
    {
        JsonNode? body = await SendAsync(HttpMethod.Get, "admin/sessions", null, authorized: true);
        List<ApiDeviceSession> sessions = new List<ApiDeviceSession>();
        foreach (JsonNode? node in body?.AsArray() ?? new JsonArray())
        {
            sessions.Add(new ApiDeviceSession(
                node!["memberId"]!.GetValue<string>(),
                node["name"]!.GetValue<string>(),
                ReadTime(node["createdUtc"]) ?? DateTime.MinValue,
                ReadTime(node["lastSeenUtc"]) ?? DateTime.MinValue));
        }

        return sessions;
    }

    public Task AdminSignOutAsync(string memberId) => SendAsync(HttpMethod.Post, "admin/members/" + memberId + "/signout", null, authorized: true);

    public async Task<List<ApiActivity>> AdminActivityAsync(int limit)
    {
        JsonNode? body = await SendAsync(HttpMethod.Get, "admin/activity?limit=" + limit.ToString(CultureInfo.InvariantCulture), null, authorized: true);
        List<ApiActivity> rows = new List<ApiActivity>();
        foreach (JsonNode? node in body?.AsArray() ?? new JsonArray())
        {
            rows.Add(new ApiActivity(
                node!["seq"]!.GetValue<long>(),
                ReadTime(node["atUtc"]) ?? DateTime.MinValue,
                node["kind"]!.GetValue<string>(),
                node["deviceId"]!.GetValue<string>(),
                node["name"]!.GetValue<string>()));
        }

        return rows;
    }

    /// <summary>Uploads changes made on the book version <paramref name="epoch"/>. Throws "book-reset" when the book was reset since.</summary>
    public async Task<PushResult> PushAsync(IReadOnlyList<BookEvent> events, string epoch)
    {
        StringBuilder json = new StringBuilder("[");
        for (int i = 0; i < events.Count; i++)
        {
            json.Append(i == 0 ? string.Empty : ",").Append(BookJsonContext.Serialize(events[i]));
        }

        json.Append(']');
        JsonNode? body = await SendRawAsync(HttpMethod.Post, "events", json.ToString(), authorized: true, bookEpoch: epoch);
        List<string> accepted = new List<string>();
        foreach (JsonNode? id in body?["accepted"]?.AsArray() ?? new JsonArray())
        {
            accepted.Add(id!.GetValue<string>());
        }

        List<(string, string)> rejected = new List<(string, string)>();
        foreach (JsonNode? item in body?["rejected"]?.AsArray() ?? new JsonArray())
        {
            rejected.Add((item?["id"]?.GetValue<string>() ?? string.Empty, item?["reason"]?.GetValue<string>() ?? string.Empty));
        }

        return new PushResult(accepted, rejected);
    }

    /// <summary>
    /// Events after a server sequence number, oldest first, and the book version they belong to.
    /// Unreadable events (from a newer app) come back as null.
    /// </summary>
    public async Task<(string Epoch, List<(long Seq, BookEvent? Event)> Rows)> PullAsync(long after, int limit)
    {
        JsonNode? body = await SendAsync(
            HttpMethod.Get,
            "events?after=" + after.ToString(CultureInfo.InvariantCulture) + "&limit=" + limit.ToString(CultureInfo.InvariantCulture),
            null,
            authorized: true);

        List<(long, BookEvent?)> rows = new List<(long, BookEvent?)>();
        foreach (JsonNode? row in body?["events"]?.AsArray() ?? new JsonArray())
        {
            long seq = row!["seq"]!.GetValue<long>();
            BookEvent? bookEvent;
            try
            {
                bookEvent = BookJsonContext.Deserialize(row["event"]!.ToJsonString());
            }
            catch (System.Exception ex) when (ex is JsonException || ex is System.NotSupportedException)
            {
                bookEvent = null;
            }

            rows.Add((seq, bookEvent));
        }

        return (body?["epoch"]?.GetValue<string>() ?? string.Empty, rows);
    }

    /// <summary>The server book's version, or null when there's no server to ask.</summary>
    public async Task<string?> EpochAsync()
    {
        try
        {
            JsonNode? body = await SendAsync(HttpMethod.Get, "status", null, authorized: false);
            return body?["epoch"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is ApiException || ex is JsonException || ex is TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Empties the book on the server for everyone (Admins only). Returns the book's new version.</summary>
    public async Task<string> ResetBookAsync(string password, bool removeMembers)
    {
        JsonNode? body = await SendAsync(
            HttpMethod.Post,
            "admin/reset",
            new JsonObject { ["password"] = password, ["removeMembers"] = removeMembers },
            authorized: true);
        return body!["epoch"]!.GetValue<string>();
    }

    private static DateTime? ReadTime(JsonNode? node)
    {
        string? text = node?.GetValue<string>();
        return text != null && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime value)
            ? value
            : null;
    }

    private static ApiSession ReadSession(JsonNode? body)
    {
        return new ApiSession(body!["token"]!.GetValue<string>(), ReadMember(body["member"]), body["epoch"]?.GetValue<string>() ?? string.Empty);
    }

    private static ApiMember ReadMember(JsonNode? node)
    {
        return new ApiMember(
            node!["id"]!.GetValue<string>(),
            node["name"]!.GetValue<string>(),
            System.Enum.Parse<TeamRole>(node["role"]!.GetValue<string>()),
            node["active"]?.GetValue<bool>() ?? true);
    }

    private Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonObject? content, bool authorized)
    {
        return SendRawAsync(method, path, content?.ToJsonString(), authorized);
    }

    private async Task<JsonNode?> SendRawAsync(HttpMethod method, string path, string? json, bool authorized, string? bookEpoch = null)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, baseUri + "api/" + path);
        if (!string.IsNullOrEmpty(bookEpoch))
        {
            request.Headers.Add("X-Book-Epoch", bookEpoch);
        }

        if (authorized && book.Settings.Token.Length > 0)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", book.Settings.Token);
        }

        if (json != null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await http.SendAsync(request);
        string text = await response.Content.ReadAsStringAsync();
        string mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        bool isJson = mediaType.Contains("json", System.StringComparison.OrdinalIgnoreCase);

        if (!response.IsSuccessStatusCode)
        {
            string code = response.StatusCode == HttpStatusCode.TooManyRequests ? "too-many-attempts" : "server-error";
            if (isJson && text.Length > 0)
            {
                code = JsonNode.Parse(text)?["error"]?.GetValue<string>() ?? code;
            }

            throw new ApiException((int)response.StatusCode, code);
        }

        // Without a sync server, the app's own fallback page (HTML) answers instead.
        if (text.Length > 0 && !isJson)
        {
            throw new ApiException((int)response.StatusCode, "no-server");
        }

        return text.Length == 0 ? null : JsonNode.Parse(text);
    }
}
