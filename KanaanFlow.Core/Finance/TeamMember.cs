namespace KanaanFlow.Core.Finance;

public sealed class TeamMember
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TeamRole Role { get; set; } = TeamRole.Staff;

    public bool CanRecord => Role != TeamRole.Viewer;

    public bool CanManage => Role == TeamRole.Owner;
}
