using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Providers;

public sealed class BisOptions
{
    public const string SectionName = "Bis";
    public string BaseUrl { get; set; } = "https://stats.bis.org/api/v2/";
}

/// <summary>
/// Central bank policy rates from the BIS.
///
/// The single highest-value integration in C3b: <c>WS_CBPOL</c> carries every
/// central bank's policy rate in ONE dataflow, monthly, end-of-period. Seven
/// national central bank sites would have been seven scrapers; this is one
/// client, and RATE is the heaviest factor in the Forex profile at weight 31.
///
/// No API key. Verified current for XM, GB, JP, AU, CH, CA and NZ.
/// </summary>
/// <remarks>
/// <c>seriesId</c> encoding: the BIS reference area code — <c>XM</c> for the euro
/// area, otherwise ISO-3166 alpha-2 (<c>GB</c>, <c>JP</c>, <c>AU</c>, <c>CH</c>,
/// <c>CA</c>, <c>NZ</c>). Not ISO-3 and not the currency code.
/// </remarks>
public sealed class BisClient(HttpClient http, ILogger<BisClient> logger) : IReleaseProvider
{
    public DataSource Provider => DataSource.Bis;

    /// <summary>Open data, no credentials.</summary>
    public bool IsConfigured => true;

    public async Task<IReadOnlyList<ProviderObservation>> GetObservationsAsync(
        string seriesId, DateOnly from, CancellationToken ct = default)
    {
        var url =
            $"data/dataflow/BIS/WS_CBPOL/1.0/M.{Uri.EscapeDataString(seriesId)}" +
            $"?format=csv&startPeriod={from:yyyy-MM}";

        var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("BIS request for {Area} failed with {Status}.", seriesId, response.StatusCode);
            throw new HttpRequestException($"BIS returned {(int)response.StatusCode} for {seriesId}.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = ParseCsv(body);

        logger.LogInformation("BIS {Area}: {Count} usable readings.", seriesId, result.Count);
        return result;
    }

    /// <summary>Separated from the HTTP call so the parsing rules are testable.</summary>
    internal static IReadOnlyList<ProviderObservation> ParseCsv(string body)
    {
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return [];

        var header = SdmxCsv.SplitRow(lines[0]);
        var timeIndex = SdmxCsv.ColumnIndex(header, "TIME_PERIOD");
        var valueIndex = SdmxCsv.ColumnIndex(header, "OBS_VALUE");
        if (timeIndex < 0 || valueIndex < 0) return [];

        var readings = new Dictionary<DateOnly, decimal>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SdmxCsv.SplitRow(line.TrimEnd('\r'));
            if (fields.Count <= Math.Max(timeIndex, valueIndex)) continue;

            var period = SdmxCsv.ParsePeriod(fields[timeIndex]);
            if (period is null) continue;

            if (!decimal.TryParse(fields[valueIndex], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                continue;

            // Last row wins per period: BIS revisions arrive as later rows for
            // the same period rather than as a revision flag.
            readings[period.Value] = value;
        }

        return readings
            .OrderBy(kv => kv.Key)
            .Select(kv => new ProviderObservation(kv.Key, kv.Value))
            .ToList();
    }
}
