using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Providers;

public sealed class OecdOptions
{
    public const string SectionName = "Oecd";
    public string BaseUrl { get; set; } = "https://sdmx.oecd.org/public/rest/";
}

/// <summary>
/// Headline CPI from the OECD, for the currencies Eurostat does not cover.
///
/// Uses <c>DSD_PRICES@DF_PRICES_ALL</c> with methodology <c>N</c> (national CPI,
/// the figure traders quote) rather than the HICP variant, and transformation
/// <c>GY</c> — already a year-over-year percent, so no transform is applied on
/// ingestion.
/// </summary>
/// <remarks>
/// <para>
/// <c>seriesId</c> encoding: <c>{dataset}:{args}</c> — <c>cpi:GBR.M</c>,
/// <c>cpi:NZL.Q</c>, <c>unemployment:GBR</c>. CPI carries a frequency because
/// New Zealand publishes quarterly while the others publish monthly.
/// </para>
/// <para>
/// CPI is not usable for EUR, CHF or JPY: this dataflow's euro-area and Swiss
/// series stop at 2025-12 and Japan's at 2021-06. Unemployment is the mirror —
/// Japan is current there, which is what closes the JPY labour gap. Both were
/// verified against the live API before the mappings were written; the OECD
/// backfills at a different pace per country and per dataset.
/// </para>
/// </remarks>
public sealed class OecdClient(HttpClient http, ILogger<OecdClient> logger) : IReleaseProvider
{
    public DataSource Provider => DataSource.Oecd;

    public bool IsConfigured => true;

    public async Task<IReadOnlyList<ProviderObservation>> GetObservationsAsync(
        string seriesId, DateOnly from, CancellationToken ct = default)
    {
        var (dataflow, key, quarterly) = Resolve(seriesId);
        var start = quarterly ? $"{from:yyyy}-Q1" : $"{from:yyyy-MM}";

        var url = $"data/{dataflow}/{key}?startPeriod={start}&format=csv";

        var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("OECD request for {Series} failed with {Status}.", seriesId, response.StatusCode);
            throw new HttpRequestException($"OECD returned {(int)response.StatusCode} for {seriesId}.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        // The OECD answers 200 with this body rather than 404 for an empty
        // selection, so it has to be detected here or it parses as zero rows.
        if (body.StartsWith("NoRecordsFound", StringComparison.Ordinal))
        {
            logger.LogWarning("OECD returned no records for {Series}.", seriesId);
            return [];
        }

        var result = BisClient.ParseCsv(body);
        logger.LogInformation("OECD {Series}: {Count} usable readings.", seriesId, result.Count);
        return result;
    }

    /// <summary>
    /// Dataflow and fully-specified SDMX key per dataset.
    ///
    /// Keys are written out in full rather than assembled from parts: a wrong
    /// dimension COUNT returns a 400, which fails loudly, while a wrong dimension
    /// VALUE returns a different statistic that looks perfectly plausible.
    /// </summary>
    internal static (string Dataflow, string Key, bool Quarterly) Resolve(string seriesId)
    {
        var parts = seriesId.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException(
                $"OECD series id must be '{{dataset}}:{{args}}', got '{seriesId}'.");

        var (dataset, args) = (parts[0], parts[1]);

        switch (dataset)
        {
            case "cpi":
            {
                var cpiParts = args.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (cpiParts.Length != 2)
                    throw new InvalidOperationException(
                        $"OECD cpi args must be '{{ISO3}}.{{FREQ}}', got '{args}'.");

                var (area, freq) = (cpiParts[0], cpiParts[1]);

                // REF_AREA.FREQ.METHODOLOGY.MEASURE.UNIT_MEASURE.EXPENDITURE
                // .ADJUSTMENT.TRANSFORMATION — methodology N is the national CPI
                // traders quote, GY is already a year-over-year percent.
                return (
                    "OECD.SDD.TPS,DSD_PRICES@DF_PRICES_ALL,1.0",
                    $"{area}.{freq}.N.CPI.PA._T.N.GY",
                    freq == "Q");
            }

            case "unemployment":
                // REF_AREA.MEASURE.UNIT_MEASURE.TRANSFORMATION.ADJUSTMENT.SEX
                // .AGE.ACTIVITY.FREQ — seasonally adjusted (Y), both sexes,
                // 15 and over, monthly.
                return (
                    "OECD.SDD.TPS,DSD_LFS@DF_IALFS_UNE_M,1.0",
                    $"{args}.UNE_LF_M.PT_LF_SUB._Z.Y._T.Y_GE15..M",
                    false);

            default:
                throw new InvalidOperationException($"Unknown OECD dataset '{dataset}'.");
        }
    }
}
