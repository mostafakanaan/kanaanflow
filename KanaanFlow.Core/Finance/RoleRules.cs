namespace KanaanFlow.Core.Finance;

/// <summary>
/// What each role may do, shared by the app (what it shows) and the sync server (what it accepts).
/// </summary>
public static class RoleRules
{
    /// <summary>Roles offered when adding or changing a member, highest first.</summary>
    public static readonly TeamRole[] Ordered = { TeamRole.Admin, TeamRole.Owner, TeamRole.Accountant, TeamRole.Staff, TeamRole.Viewer };

    public static bool CanRecord(TeamRole role) => role != TeamRole.Viewer;

    /// <summary>Categories, entities, opening balances and undoing anyone's entries.</summary>
    public static bool CanManageBook(TeamRole role) => role is TeamRole.Admin or TeamRole.Owner or TeamRole.Accountant;

    public static bool CanManageMembers(TeamRole role) => CanManageBook(role);

    public static bool CanApprove(TeamRole role) => role is TeamRole.Admin or TeamRole.Accountant;

    /// <summary>Marking entries as deleted, and restoring them.</summary>
    public static bool CanDelete(TeamRole role) => role is TeamRole.Admin or TeamRole.Accountant;

    /// <summary>Deleting entries for good.</summary>
    public static bool CanPurge(TeamRole role) => role == TeamRole.Admin;

    /// <summary>Entries recorded by Owners show whether an Accountant has approved them.</summary>
    public static bool NeedsApproval(TeamRole role) => role == TeamRole.Owner;

    /// <summary>Employee is not offered yet.</summary>
    public static bool IsAvailable(TeamRole role) => role != TeamRole.Staff;

    /// <summary>Whether <paramref name="actor"/> may give a member the role <paramref name="role"/>.</summary>
    public static bool CanAssign(TeamRole actor, TeamRole role)
    {
        if (!CanManageMembers(actor) || !IsAvailable(role))
        {
            return false;
        }

        return actor == TeamRole.Admin || role != TeamRole.Admin;
    }

    /// <summary>Whether <paramref name="actor"/> may change a member who has <paramref name="target"/> (role, password, removal).</summary>
    public static bool CanEdit(TeamRole actor, TeamRole target)
    {
        return CanManageMembers(actor) && (actor == TeamRole.Admin || target != TeamRole.Admin);
    }
}
