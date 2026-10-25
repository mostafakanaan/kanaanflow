namespace KanaanFlow.Core.Finance;

/// <summary>
/// The currencies the book tracks. Each keeps its own balance and is never converted silently.
/// </summary>
public enum Currency
{
    SYP,
    USD,
    EUR,
    TRY
}
