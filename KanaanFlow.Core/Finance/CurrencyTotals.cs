namespace KanaanFlow.Core.Finance;

/// <summary>
/// Money in and out for one currency.
/// </summary>
public sealed class CurrencyTotals
{
    public CurrencyTotals(Currency currency, decimal totalIn, decimal totalOut)
    {
        Currency = currency;
        In = totalIn;
        Out = totalOut;
    }

    public Currency Currency { get; }

    public decimal In { get; }

    public decimal Out { get; }

    public decimal Net => In - Out;

    public bool HasActivity => In != 0m || Out != 0m;
}
