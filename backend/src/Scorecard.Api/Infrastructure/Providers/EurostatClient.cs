using System.Globalization;
using System.Text.Json;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Providers;

public sealed class EurostatOptions
{
    public const string SectionName = "Eurostat";
    public string BaseUrl { get; set; } = "https://ec.europa.eu/eurostat/api/dissemination/";
}

/// <summary>
/// Harmonised CPI from Eurostat, for the euro area and Switzerland.
///
/// Uses <c>prc_hicp_minr</c> — the ECOICOP ver.2 dataset. The obvious candidates
/// are all dead ends and were each checked against the live API first:
/// <c>prc_hicp_manr</c> is archived at 2025-12, and the ECB's own <c>ICP</c>
/// dataflow was discontinued in February 2026 when Eurostat changed methodology.
/// This is the successor and is current.
/// </summary>
/// <remarks>
/// <para>
/// <c>seriesId</c> encoding: <c>{dataset}:{geo}</c> — <c>hicp:EA21</c>,
/// <c>unemployment:CH</c>. The dataset prefix exists because each Eurostat
/// dataset needs its own filter dimensions, and those cannot be inferred from a
/// geo code alone.
/// </para>
/// <para>
/// Response is JSON-stat: a flat <c>value</c> map keyed by observation index,
/// with the index meaning position in the (single) time dimension. Sparse by
/// design — a missing index means the country has not published that month yet,
/// which is why Switzerland's unemployment trails the euro area by three months.
/// </para>
/// </remarks>
public sealed class EurostatClient(HttpClient http, ILogger<EurostatClient> logger) : IReleaseProvider
{
    public DataSource Provider => DataSource.Eurostat;

    public bool IsConfigured => true;

    public async Task<IReadOnlyList<ProviderObservation>> GetObservationsAsync(
        string seriesId, DateOnly from, CancellationToken ct = default)
    {
        var (dataset, filters, geo) = Resolve(seriesId);

        var url =
            $"statistics/1.0/data/{dataset}?format=JSON" +
            $"&{filters}" +
            $"&geo={Uri.EscapeDataString(geo)}" +
            $"&sinceTimePeriod={from:yyyy-MM}";

        var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Eurostat request for {Series} failed with {Status}.", seriesId, response.StatusCode);
            throw new HttpRequestException($"Eurostat returned {(int)response.StatusCode} for {seriesId}.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = ParseJsonStat(body);

        logger.LogInformation("Eurostat {Series}: {Count} usable readings.", seriesId, result.Count);
        return result;
    }

    /// <summary>
    /// Dataset code and its non-geo filters. Kept as a switch rather than stored
    /// per row because the filters are a property of the dataset, not of the
    /// country — every geo in a dataset needs the same ones.
    /// </summary>
    internal static (string Dataset, string Filters, string Geo) Resolve(string seriesId)
    {
        var parts = seriesId.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException(
                $"Eurostat series id must be '{{dataset}}:{{geo}}', got '{seriesId}'.");

        var (dataset, geo) = (parts[0], parts[1]);

        return dataset switch
        {
            // ECOICOP ver.2 HICP, all-items, annual rate of change.
            "hicp" => ("prc_hicp_minr", "coicop18=TOTAL&unit=RCH_A", geo),

            // Harmonised ILO unemployment rate, seasonally adjusted, all ages,
            // both sexes, as a percent of the active population.
            "unemployment" => ("une_rt_m", "s_adj=SA&age=TOTAL&sex=T&unit=PC_ACT", geo),

            _ => throw new InvalidOperationException($"Unknown Eurostat dataset '{dataset}'.")
        };
    }

    /// <summary>Separated from the HTTP call so the JSON-stat shape is testable.</summary>
    internal static IReadOnlyList<ProviderObservation> ParseJsonStat(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        // Eurostat answers 200 with an error envelope for an invalid selection.
        if (root.TryGetProperty("error", out _)) return [];

        if (!root.TryGetProperty("dimension", out var dimension)
            || !dimension.TryGetProperty("time", out var time)
            || !time.TryGetProperty("category", out var timeCategory)
            || !timeCategory.TryGetProperty("index", out var timeIndex))
            return [];

        // index maps period -> position; invert it so a value key resolves back
        // to a date without assuming the JSON preserves order.
        var periods = new Dictionary<int, DateOnly>();
        foreach (var entry in timeIndex.EnumerateObject())
        {
            if (!entry.Value.TryGetInt32(out var position)) continue;
            var period = SdmxCsv.ParsePeriod(entry.Name);
            if (period is not null) periods[position] = period.Value;
        }

        if (!root.TryGetProperty("value", out var values)) return [];

        var readings = new List<ProviderObservation>();

        foreach (var entry in values.EnumerateObject())
        {
            if (!int.TryParse(entry.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
                continue;
            if (!periods.TryGetValue(position, out var period)) continue;

            // Nulls appear for months a country has not published. Skipping is
            // the same rule FredClient applies to "." — a gap must never be
            // read as a zero, which would poison the level percentile.
            if (entry.Value.ValueKind is JsonValueKind.Null) continue;
            if (!entry.Value.TryGetDecimal(out var value)) continue;

            readings.Add(new ProviderObservation(period, value));
        }

        return readings.OrderBy(r => r.Date).ToList();
    }
}
