using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Providers;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Ingestion;

public sealed record SyncReleaseResult(
    string IndicatorCode,
    string Currency,
    string Provider,
    string ProviderSeriesId,
    string Transform,
    int Derived,
    int Inserted,
    int Updated,
    int SeedRowsRemoved,
    DateOnly? LatestPeriod,
    decimal? LatestValue);

public sealed record SyncReleasesResponse(IReadOnlyList<SyncReleaseResult> Indicators, DateTimeOffset SyncedAt);

/// <summary>
/// Ingests economic releases from whichever provider backs each
/// (indicator, currency) pair — phase C3b.
///
/// C3a could hardcode USD because FRED was the only provider. It no longer is:
/// policy rates come from the BIS for all seven non-USD currencies, and CPI
/// splits between Eurostat and the OECD purely because neither is current for
/// every country. The mapping is data (<see cref="IndicatorSource"/>), so adding
/// a currency is a seed row rather than a code change.
///
/// Deletion of superseded fixture rows stays scoped to the exact
/// (indicator, currency) being synced. That is what lets a currency be partly
/// real: the EUR policy rate and CPI become genuine while EUR retail sales keep
/// their fixture, instead of the whole currency having to move at once.
/// </summary>
public sealed class SyncReleasesHandler(
    AppDbContext db,
    ReleaseProviderRegistry providers,
    IOptions<FredOptions> options,
    ILogger<SyncReleasesHandler> logger)
{
    public async Task<SyncReleasesResponse> HandleAsync(
        string? indicatorCode, string? currency, CancellationToken ct)
    {
        var query = db.IndicatorSources
            .Include(s => s.Indicator)
            .Where(s => s.IsEnabled);

        if (!string.IsNullOrWhiteSpace(indicatorCode))
            query = query.Where(s => s.Indicator!.Code == indicatorCode);

        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(s => s.CurrencyCode == currency);

        var sources = await query.ToListAsync(ct);
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-options.Value.HistoryDays * 4);
        var results = new List<SyncReleaseResult>();

        foreach (var source in sources)
        {
            var result = await SyncOneAsync(source, source.Indicator!, from, ct);
            if (result is not null) results.Add(result);
        }

        return new SyncReleasesResponse(results, DateTimeOffset.UtcNow);
    }

    private async Task<SyncReleaseResult?> SyncOneAsync(
        IndicatorSource source, Indicator indicator, DateOnly from, CancellationToken ct)
    {
        var provider = providers.Resolve(source.Provider);

        var seriesIds = source.ProviderSeriesId
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var fetched = new List<IReadOnlyList<ProviderObservation>>();
        foreach (var id in seriesIds)
            fetched.Add(await provider.GetObservationsAsync(id, from, ct));

        var combined = SeriesTransforms.Blend(fetched);
        var derived = SeriesTransforms.Apply(source.Transform, combined);

        if (derived.Count == 0)
        {
            logger.LogWarning(
                "No usable readings derived for {Code}/{Currency} from {Provider}; leaving existing rows alone.",
                indicator.Code, source.CurrencyCode, source.Provider);
            return null;
        }

        // Real data supersedes the fixture for this FACTOR and this currency —
        // not merely for this indicator.
        //
        // Several indicators feed one factor: NFP, employment change and the
        // unemployment rate all feed LABOUR. Scoping the delete to the indicator
        // would leave the fixture NFP rows in place alongside the real
        // unemployment rows, and the loader picks one release per
        // (factor, currency) — with equal periods the tiebreak between two
        // different indicators is arbitrary. Scoping to the factor makes the
        // supersede total and the selection deterministic.
        var factorIndicatorIds = await db.Indicators
            .Where(i => i.FactorCode == indicator.FactorCode)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var removed = await db.IndicatorReleases
            .Where(r => factorIndicatorIds.Contains(r.IndicatorId)
                        && r.CurrencyCode == source.CurrencyCode
                        && r.Source == DataSource.Manual)
            .ExecuteDeleteAsync(ct);

        var existing = await db.IndicatorReleases
            .Where(r => r.IndicatorId == indicator.Id && r.CurrencyCode == source.CurrencyCode)
            .ToDictionaryAsync(r => r.Period, ct);

        var inserted = 0;
        var updated = 0;
        var ordered = derived.OrderBy(d => d.Period).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var reading = ordered[i];
            var previous = i > 0 ? ordered[i - 1].Value : (decimal?)null;

            // Providers stamp a period, not a publication instant. The period END
            // is the closest defensible approximation — see SdmxCsv.PeriodEnd for
            // why the start is not merely imprecise but actively wrong.
            var releasedAt = new DateTimeOffset(
                SdmxCsv.PeriodEnd(reading.Period, source.Cadence).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);

            if (existing.TryGetValue(reading.Period, out var row))
            {
                // ReleasedAt is compared too: rows written before the period-end
                // fix carry a start-of-period stamp and must be corrected, or the
                // back-dating survives every future sync.
                if (row.Actual != reading.Value || row.Previous != previous || row.ReleasedAt != releasedAt)
                {
                    row.Actual = reading.Value;
                    row.Previous = previous;
                    row.ReleasedAt = releasedAt;
                    row.Source = source.Provider;
                    row.SourceRef = source.ProviderSeriesId;
                    row.ImportedAt = DateTimeOffset.UtcNow;
                    updated++;
                }
                continue;
            }

            db.IndicatorReleases.Add(new IndicatorRelease
            {
                Id = Guid.NewGuid(),
                IndicatorId = indicator.Id,
                CurrencyCode = source.CurrencyCode,
                Period = reading.Period,
                Actual = reading.Value,
                // None of the four providers carries a survey consensus.
                // v2.0.0 does not need one.
                Forecast = null,
                Previous = previous,
                Revision = 0,
                Source = source.Provider,
                SourceRef = source.ProviderSeriesId,
                ReleasedAt = releasedAt,
                ImportedAt = DateTimeOffset.UtcNow
            });
            inserted++;
        }

        await db.SaveChangesAsync(ct);

        var latest = ordered[^1];
        return new SyncReleaseResult(
            indicator.Code, source.CurrencyCode, source.Provider.ToString(),
            source.ProviderSeriesId, source.Transform.ToString(),
            derived.Count, inserted, updated, removed, latest.Period, latest.Value);
    }
}

public sealed class SyncReleasesEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/admin/releases/sync", async (
                SyncReleasesHandler handler, string? indicatorCode, string? currency, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(indicatorCode, currency, ct)))
            .WithName("SyncReleases")
            .WithTags("Admin")
            .Produces<SyncReleasesResponse>();
}
