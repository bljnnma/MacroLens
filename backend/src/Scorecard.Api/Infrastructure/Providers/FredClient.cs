using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Scorecard.Api.Infrastructure.Providers;

public sealed class FredOptions
{
    public const string SectionName = "Fred";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.stlouisfed.org/";

    /// <summary>Two years comfortably covers the 252-observation percentile window.</summary>
    public int HistoryDays { get; set; } = 760;
}

/// <summary>
/// Reads observation series from FRED. Free, official, and — unlike calendar
/// vendors — carries no redistribution restriction that would complicate showing
/// the data to end users.
/// </summary>
public sealed class FredClient(HttpClient http, IOptions<FredOptions> options, ILogger<FredClient> logger)
    : IReleaseProvider
{
    private readonly FredOptions _options = options.Value;

    public Domain.DataSource Provider => Domain.DataSource.Fred;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <remarks>
    /// <c>seriesId</c> encoding: a FRED series id, or several comma-separated for
    /// a blend (the PMI proxy).
    /// </remarks>
    public async Task<IReadOnlyList<ProviderObservation>> GetObservationsAsync(
        string seriesId, DateOnly from, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Fred:ApiKey is not configured.");

        var url =
            $"fred/series/observations?series_id={Uri.EscapeDataString(seriesId)}" +
            $"&api_key={Uri.EscapeDataString(_options.ApiKey)}" +
            $"&file_type=json&sort_order=asc" +
            $"&observation_start={from:yyyy-MM-dd}";

        var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Never log the URL: it carries the API key as a query parameter.
            logger.LogError("FRED request for {SeriesId} failed with {Status}.", seriesId, response.StatusCode);
            throw new HttpRequestException($"FRED returned {(int)response.StatusCode} for {seriesId}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<FredResponse>(ct);
        if (payload?.Observations is null) return [];

        var result = new List<ProviderObservation>(payload.Observations.Count);

        foreach (var row in payload.Observations)
        {
            // FRED encodes "no value" as "." — holidays, non-trading days, and
            // genuine gaps all arrive this way. Parsing it as zero would poison
            // the percentile window.
            if (string.IsNullOrWhiteSpace(row.Value) || row.Value == ".") continue;

            if (!decimal.TryParse(row.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                continue;

            if (!DateOnly.TryParse(row.Date, CultureInfo.InvariantCulture, out var date))
                continue;

            result.Add(new ProviderObservation(date, value));
        }

        logger.LogInformation(
            "FRED {SeriesId}: {Usable} usable of {Total} rows.",
            seriesId, result.Count, payload.Observations.Count);

        return result;
    }

    private sealed record FredResponse(
        [property: JsonPropertyName("observations")] List<FredRow>? Observations);

    private sealed record FredRow(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("value")] string Value);
}
