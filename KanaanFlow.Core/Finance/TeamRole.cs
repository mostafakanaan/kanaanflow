namespace KanaanFlow.Core.Finance;

/// <summary>
/// What a team member may do. The numbers are stored in older events, so new roles are only ever added at the end.
/// </summary>
public enum TeamRole
{
    /// <summary>Everything on the book and the team except Admins. Their entries wait for an Accountant's approval.</summary>
    Owner,

    /// <summary>Employee: records entries and undoes only their own. Not offered yet (salaries come later).</summary>
    Staff,

    /// <summary>Read-only: sees balances and reports only.</summary>
    Viewer,

    /// <summary>Everything, including other Admins and the admin panel.</summary>
    Admin,

    /// <summary>Same as an Owner, and approves Owners' entries.</summary>
    Accountant
}
