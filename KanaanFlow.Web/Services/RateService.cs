namespace KanaanFlow.Web.Services;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance;

/// <summary>
/// Looks up a current exchange rate online. The rate is only a suggestion: the person can always type their own.
/// </summary>
public sealed class RateService
{
    public const string SourceName = "open.er-api.com";

    private readonly HttpClient http;

    public RateService(HttpClient http)
    {
        this.http = http;
    }

    /// <summary>1 unit of <paramref name="from"/> in <paramref name="to"/>, or null when it can't be fetched.</summary>
    public async Task<decimal?> GetRateAsync(Currency from, Currency to)
    {
        try
        {
            string json = await http.GetStringAsync("https://open.er-api.com/v6/latest/" + from);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("rates", out JsonElement rates) && rates.TryGetProperty(to.ToString(), out JsonElement rate))
            {
                decimal value = rate.GetDecimal();
                return value > 0m ? Math.Round(value, value >= 1m ? 4 : 6) : null;
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is TaskCanceledException || ex is FormatException)
        {
            return null;
        }
    }
}
