namespace KanaanFlow.Core.Helpers;

using KanaanFlow.Core.Enums;

public static class CurrencyHelper
{
    public static string GetSymbol(Currency currency) => currency switch
    {
        Currency.USD => "$",
        Currency.EUR => "€",
        Currency.TL => "₺",
        Currency.SP => "ل.س",
        _ => "$"
    };

    public static string Format(decimal amount, Currency currency) => currency switch
    {
        Currency.USD => $"${amount:N2}",
        Currency.EUR => $"€{amount:N2}",
        Currency.TL => $"₺{amount:N2}",
        Currency.SP => $"{amount:N2} ل.س",
        _ => $"${amount:N2}"
    };
}
