using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;

namespace Scorecard.Api.Scoring;

public sealed record ScoringDataSet(
    IReadOnlyDictionary<string, Factor> Factors,
    IReadOnlyList<Asset> Assets,
    IReadOnlyDictionary<Market, ScoringProfile> Profiles,
    IReadOnlyDictionary<(string FactorCode, string Currency), ReleaseSnapshot> Releases,
    IReadOnlyDictionary<string, SeriesSnapshot> Series,
    IReadOnlyList<string> Currencies,
    IReadOnlyDictionary<string, CurrencyPolicy> Policies,
    DateTimeOffset DataAsOf);

/// <summary>
/// Loads the entire working set in one pass, before any contributor runs.
///
/// This is what makes rule R1 enforceable: contributors receive data, never a
/// DbContext. It also removes N+1 queries as a side effect — the engine issues a
/// fixed number of queries regardless of how many assets are scored.
/// </summary>
public sealed class ScoringDataLoader(AppDbContext db)
{
    /// <summary>Trailing window for series percentile factors — roughly one trading year.</summary>
    public const int SeriesWindow = 252;

    /// <summary>
    /// How far back a release may sit and still be FOUND. Only has to exceed the
    /// widest MaxAgeDays in the model (400, for policy rates), plus headroom for
    /// revisions — the contributor does the actual staleness check.
    /// </summary>
    private const int StalenessLookbackDays = 500;

    /// <summary>
    /// How much history the level percentile is measured against — five years,
    /// so roughly 60 monthly readings or 20 quarterly ones.
    ///
    /// This is the binding constraint and it used to be missing: the loader had
    /// one 500-day window serving both purposes, so a percentile was computed
    /// from about 16 monthly points even though 99 were stored. Sixteen points
    /// make each one worth six percentile points, and a label reading "0th
    /// percentile" then means "lowest of sixteen" — far weaker evidence than it
    /// sounds.
    ///
    /// Five years rather than everything ingested (~8) on purpose. A longer
    /// window reaches the April 2020 unemployment spike, and a single reading of
    /// 14.8% against a 4% norm pushes every subsequent print into the bottom of
    /// the distribution — the level component would then read "low" more or less
    /// permanently. That is a regime break contaminating the sample, not extra
    /// information.
    /// </summary>
    private const int HistoryLookbackDays = 1825;

    public async Task<ScoringDataSet> LoadAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        // History dominates, but taking the max states the dependency rather than
        // relying on one constant happening to be the larger.
        var cutoff = asOf.AddDays(-Math.Max(StalenessLookbackDays, HistoryLookbackDays));

        var factors = await db.Factors.AsNoTracking().ToListAsync(ct);
        var assets = await db.Assets.AsNoTracking()
            .Include(a => a.Exposures)
            .Where(a => a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync(ct);
        var profiles = await db.ScoringProfiles.AsNoTracking()
            .Include(p => p.Weights)
            .Where(p => p.IsActive)
            .ToListAsync(ct);
        var indicators = await db.Indicators.AsNoTracking().ToListAsync(ct);
        var releases = await db.IndicatorReleases.AsNoTracking()
            .Where(r => r.ReleasedAt >= cutoff && r.ReleasedAt <= asOf)
            .ToListAsync(ct);
        var series = await db.MarketSeries.AsNoTracking().ToListAsync(ct);
        var observations = await db.SeriesObservations.AsNoTracking()
            .Where(o => o.ObservedAt <= asOf)
            .ToListAsync(ct);
        var policies = await db.CurrencyPolicies.AsNoTracking().ToListAsync(ct);

        return Project(factors, assets, profiles, indicators, releases, series, observations, policies, asOf);
    }

    /// <summary>
    /// The selection rules, as a pure function. The loader and the test fixtures
    /// both call this, so "which release wins" cannot drift between the database
    /// path and the in-memory one.
    /// </summary>
    public static ScoringDataSet Project(
        IEnumerable<Factor> factors,
        IEnumerable<Asset> assets,
        IEnumerable<ScoringProfile> profiles,
        IEnumerable<Indicator> indicators,
        IEnumerable<IndicatorRelease> releases,
        IEnumerable<MarketSeries> series,
        IEnumerable<SeriesObservation> observations,
        IEnumerable<CurrencyPolicy> policies,
        DateTimeOffset asOf)
    {
        var indicatorById = indicators.ToDictionary(i => i.Id);
        var assetList = assets.ToList();
        var seriesList = series.ToList();

        var latestReleases = releases
            .Where(r => indicatorById.ContainsKey(r.IndicatorId) && r.ReleasedAt <= asOf)
            .GroupBy(r => (indicatorById[r.IndicatorId].FactorCode, r.CurrencyCode))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    // Pick the release first, then resolve ITS indicator. Several
                    // indicators can feed one factor (NFP for USD, employment
                    // change or unemployment elsewhere), so taking the indicator
                    // from an arbitrary group member would pair the wrong bands
                    // with the chosen release.
                    // A printed release always beats an unprinted one, regardless
                    // of period. Without this, a scheduled row whose release time
                    // has passed but whose actual has not been ingested yet wins
                    // on Period and knocks the factor out entirely — discarding a
                    // perfectly good prior print and silently collapsing coverage.
                    var winner = g
                        .OrderByDescending(r => r.Actual.HasValue)
                        .ThenByDescending(r => r.Period)
                        .ThenByDescending(r => r.Revision)
                        .ThenByDescending(r => r.ReleasedAt)
                        .First();

                    // Oldest first, one reading per period with the highest
                    // revision winning. This is the series v2.0.0 takes the
                    // level percentile within.
                    var history = g
                        .Where(r => r.Actual.HasValue && r.Period <= winner.Period)
                        .GroupBy(r => r.Period)
                        .Select(byPeriod => byPeriod.OrderByDescending(r => r.Revision).First())
                        .OrderBy(r => r.Period)
                        .Select(r => r.Actual!.Value)
                        .ToList();

                    return new ReleaseSnapshot(indicatorById[winner.IndicatorId], winner, history);
                });

        var observationsBySeries = observations
            .Where(o => o.ObservedAt <= asOf)
            .GroupBy(o => o.SeriesId)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.ObservedAt).ToList());

        var seriesSnapshots = new Dictionary<string, SeriesSnapshot>(StringComparer.Ordinal);
        foreach (var s in seriesList)
        {
            if (!observationsBySeries.TryGetValue(s.Id, out var all) || all.Count == 0) continue;

            // Newest SeriesWindow observations, oldest first: the normalizers
            // expect the current observation last.
            var window = all.Skip(Math.Max(0, all.Count - SeriesWindow)).ToList();
            seriesSnapshots[s.FactorCode] = new SeriesSnapshot(s, window);
        }

        var currencies = assetList
            .SelectMany(a => a.Exposures.Select(e => e.CurrencyCode))
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var newestRelease = latestReleases.Values
            .Select(r => r.Release.ReleasedAt)
            .DefaultIfEmpty(asOf)
            .Max();

        var newestObservation = seriesSnapshots.Values
            .Select(s => s.Window[^1].ObservedAt)
            .DefaultIfEmpty(asOf)
            .Max();

        return new ScoringDataSet(
            factors.ToDictionary(f => f.Code, StringComparer.Ordinal),
            assetList,
            profiles.ToDictionary(p => p.Market),
            latestReleases,
            seriesSnapshots,
            currencies,
            policies.ToDictionary(p => p.CurrencyCode, StringComparer.OrdinalIgnoreCase),
            newestRelease > newestObservation ? newestRelease : newestObservation);
    }

    public static ScoringContext BuildContext(
        ScoringDataSet data, Asset asset, ScoringProfile profile, DateTimeOffset asOf) =>
        new()
        {
            Asset = asset,
            Profile = profile,
            Factors = data.Factors,
            Releases = data.Releases,
            Series = data.Series,
            Currencies = data.Currencies,
            Policies = data.Policies,
            AsOfUtc = asOf
        };
}
