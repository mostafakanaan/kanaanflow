namespace KanaanFlow.Web.Services;

using System;
using System.Globalization;
using System.Text;
using KanaanFlow.Core.Finance;

/// <summary>
/// Number, currency and date formatting. Digits are always Western (123), matching the spreadsheet.
/// </summary>
public static class Fmt
{
    public static string Money(decimal value)
    {
        decimal rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("#,##0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Money with an explicit sign: "+1,500" or "−350" (true minus sign).</summary>
    public static string Signed(decimal value, bool positive)
    {
        return (positive ? "+" : "−") + Money(Math.Abs(value));
    }

    public static string Net(decimal value)
    {
        if (value > 0m)
        {
            return "+" + Money(value);
        }

        return value < 0m ? "−" + Money(-value) : "0";
    }

    /// <summary>Groups the digits of a number being typed on the keypad, keeping a trailing ".".</summary>
    public static string Typing(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "0";
        }

        string[] parts = raw.Split('.');
        string whole = parts[0].Length == 0 ? "0" : parts[0];
        StringBuilder grouped = new StringBuilder();
        for (int i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0)
            {
                grouped.Append(',');
            }

            grouped.Append(whole[i]);
        }

        return parts.Length > 1 ? grouped + "." + parts[1] : grouped.ToString();
    }

    /// <summary>Reads an amount typed by a person, accepting Arabic digits and separators.</summary>
    public static decimal? ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        StringBuilder clean = new StringBuilder();
        foreach (char ch in text.Trim())
        {
            if (ch >= '٠' && ch <= '٩')
            {
                clean.Append((char)('0' + (ch - '٠')));
            }
            else if (ch >= '۰' && ch <= '۹')
            {
                clean.Append((char)('0' + (ch - '۰')));
            }
            else if (ch == '٫' || ch == '.')
            {
                clean.Append('.');
            }
            else if (char.IsDigit(ch))
            {
                clean.Append(ch);
            }
        }

        return decimal.TryParse(clean.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : null;
    }

    public static string Symbol(Currency currency, string language)
    {
        bool ar = language != "en";
        switch (currency)
        {
            case Currency.SYP:
                return ar ? "ل.س" : "SYP";
            case Currency.USD:
                return "$";
            case Currency.EUR:
                return "€";
            default:
                return ar ? "ل.ت" : "₺";
        }
    }

    public static string ShortName(Currency currency, string language)
    {
        bool ar = language != "en";
        switch (currency)
        {
            case Currency.SYP:
                return ar ? "سورية" : "Syrian";
            case Currency.USD:
                return ar ? "دولار" : "Dollar";
            case Currency.EUR:
                return ar ? "يورو" : "Euro";
            default:
                return ar ? "تركية" : "Turkish";
        }
    }

    public static string LongName(Currency currency, string language)
    {
        bool ar = language != "en";
        switch (currency)
        {
            case Currency.SYP:
                return ar ? "ليرة سورية" : "Syrian pound";
            case Currency.USD:
                return ar ? "دولار" : "US dollar";
            case Currency.EUR:
                return ar ? "يورو" : "Euro";
            default:
                return ar ? "ليرة تركية" : "Turkish lira";
        }
    }

    /// <summary>"23 تشرين الأول" or "23 Oct", with the year only when it is not this year.</summary>
    public static string Date(DateOnly date, string language, DateOnly today)
    {
        string[] months = language == "en" ? Strings.MonthsEn : Strings.MonthsAr;
        string year = date.Year == today.Year ? string.Empty : " " + date.Year.ToString(CultureInfo.InvariantCulture);
        return date.Day.ToString(CultureInfo.InvariantCulture) + " " + months[date.Month - 1] + year;
    }

    /// <summary>"Friday, 23 October 2026".</summary>
    public static string LongDate(DateOnly date, string language)
    {
        bool en = language == "en";
        string day = (en ? Strings.DaysEn : Strings.DaysAr)[(int)date.DayOfWeek];
        string month = (en ? Strings.MonthsEnLong : Strings.MonthsAr)[date.Month - 1];
        return day + (en ? ", " : "، ") + date.Day.ToString(CultureInfo.InvariantCulture) + " " + month + " " + date.Year.ToString(CultureInfo.InvariantCulture);
    }

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? ParseIso(string? text)
    {
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
    }

    public static string Initial(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length == 0 ? "?" : char.ToUpperInvariant(trimmed[0]).ToString();
    }
}
