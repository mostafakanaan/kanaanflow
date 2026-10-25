namespace KanaanFlow.Core.Finance;

using System.Text.Json.Serialization;

public sealed class TeamMember
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TeamRole Role { get; set; } = TeamRole.Staff;

    /// <summary>False once removed from the team; kept so their past entries still show who recorded them.</summary>
    public bool Active { get; set; } = true;

    [JsonIgnore]
    public bool CanRecord => RoleRules.CanRecord(Role);

    [JsonIgnore]
    public bool CanManageBook => RoleRules.CanManageBook(Role);

    [JsonIgnore]
    public bool CanManageMembers => RoleRules.CanManageMembers(Role);

    [JsonIgnore]
    public bool CanApprove => RoleRules.CanApprove(Role);

    [JsonIgnore]
    public bool IsAdmin => Role == TeamRole.Admin;

    public TeamMember Clone() => (TeamMember)MemberwiseClone();
}
