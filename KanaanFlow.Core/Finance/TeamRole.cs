namespace KanaanFlow.Core.Finance;

public enum TeamRole
{
    /// <summary>Everything: entries, reports, categories, team.</summary>
    Owner,

    /// <summary>Records entries and undoes only their own.</summary>
    Staff,

    /// <summary>Sees balances and reports only.</summary>
    Viewer
}
