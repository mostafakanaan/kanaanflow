namespace KanaanFlow.Sync.Contracts;

using System;
using System.Collections.Generic;

public sealed record MemberDto(string Id, string Name, string Role, bool Active);

/// <summary>Epoch: the book's version, new after every reset.</summary>
public sealed record StatusResponse(bool Ready, string Epoch);

public sealed record RegisterRequest(string Name, string Password, string MasterPassword, string? MemberId);

public sealed record LoginRequest(string Name, string Password);

public sealed record ResetPasswordRequest(string Name, string MasterPassword, string NewPassword);

public sealed record SessionResponse(string Token, MemberDto Member, string Epoch);

public sealed record CreateMemberRequest(string Name, string Role, string Password);

public sealed record UpdateMemberRequest(string? Role, string? Password, bool? Active);

public sealed record ChangePasswordRequest(string Current, string New);

public sealed record RejectedEvent(string Id, string Reason);

public sealed record PushResponse(List<string> Accepted, List<RejectedEvent> Rejected);

public sealed record ErrorResponse(string Error);

public sealed record ResetBookRequest(string Password, bool RemoveMembers);

public sealed record ResetBookResponse(string Epoch, int ArchivedEvents, int RemovedMembers);

public sealed record AdminOverview(long Members, long Events, long Sessions, DateTime? LastChangeUtc);

public sealed record AdminSession(string MemberId, string Name, DateTime CreatedUtc, DateTime LastSeenUtc);

public sealed record AdminActivity(long Seq, DateTime AtUtc, DateTime ReceivedUtc, string Kind, string DeviceId, string Name);
