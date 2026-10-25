namespace KanaanFlow.Core.Finance;

public enum EntryType
{
    /// <summary>Money received.</summary>
    In,

    /// <summary>Money paid out.</summary>
    Out,

    /// <summary>One currency swapped for another at a saved rate.</summary>
    Exchange
}
