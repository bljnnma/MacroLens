using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database.ReadModel;

/// <summary>How much of an asset's scoring weight rests on provider data.</summary>
public sealed record AssetProvenance(bool IsFullyReal, decimal RealShare)
{
    public static readonly AssetProvenance None = new(false, 0m);
}

/// <summary>
/// Which parts of each score come from a provider and which still lean on seeded
/// fixtures.
///
/// Measured per (currency, factor) rather than per currency, because C3b made
/// "is this currency real" a question with no answer: after it, EUR's policy rate
/// and CPI are genuine while EUR growth and sentiment are still fixtures. A
/// currency-level flag would have reported the euro as fully real on the strength
/// of two factors out of eight — the badge would have been claiming something the
/// data does not support, which is the one thing it exists to prevent.
///
/// Derived from row provenance, so an asset's share moves on its own as
/// integrations land, with no code change.
/// </summary>
public sealed class ProvenanceQuery(AppDbContext db)
{
    /// <summary>
    /// (currency, factor) pairs whose newest release came from a provider.
    ///
    /// Newest, not any: a factor that was ingested once and has since fallen back
    /// to fixture rows is not provider-backed, and counting it would let a dead
    /// integration keep its badge indefinitely.
    /// </summary>
    public async Task<IReadOnlySet<(string Currency, string FactorCode)>> ProviderBackedFactorsAsync(
        CancellationToken ct = default)
    {
        var rows = await db.IndicatorReleases
            .AsNoTracking()
            .Join(db.Indicators.AsNoTracking(),
                r => r.IndicatorId, i => i.Id,
                (r, i) => new { i.FactorCode, r.CurrencyCode, r.Period, r.Source })
            .ToListAsync(ct);

        var backed = rows
            .GroupBy(r => (Currency: r.CurrencyCode, r.FactorCode))
            .Where(g => g.OrderByDescending(r => r.Period).First().Source != DataSource.Manual)
            .Select(g => g.Key)
            .ToHashSet();

        // Market series are USD-scoped and, since C3a, entirely provider-backed.
        // Read from the rows rather than assumed, for the same reason as above.
        var seriesFactors = await db.MarketSeries
            .AsNoTracking()
            .Where(s => s.Source != DataSource.Manual)
            .Select(s => s.FactorCode)
            .ToListAsync(ct);

        foreach (var factorCode in seriesFactors)
            backed.Add(("USD", factorCode));

        return backed;
    }

    /// <summary>
    /// Provenance for a set of assets, keyed by asset id.
    ///
    /// One method rather than three near-identical assemblies in the endpoints:
    /// Top Setups, Markets and Asset Detail must all report the same share for
    /// the same asset, and the surest way to guarantee that is one code path.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, AssetProvenance>> EvaluateAsync(
        IEnumerable<Asset> assets, CancellationToken ct = default)
    {
        var backed = await ProviderBackedFactorsAsync(ct);

        var profiles = await db.ScoringProfiles
            .AsNoTracking()
            .Include(p => p.Weights)
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        var byMarket = profiles.ToDictionary(p => p.Market);
        var factors = await db.Factors.AsNoTracking().ToDictionaryAsync(f => f.Code, ct);

        var result = new Dictionary<Guid, AssetProvenance>();

        foreach (var asset in assets.DistinctBy(a => a.Id))
        {
            byMarket.TryGetValue(asset.Market, out var profile);
            result[asset.Id] = Evaluate(asset, profile, backed, factors);
        }

        return result;
    }

    /// <summary>
    /// Weighted share of an asset's active profile that provider data supports.
    ///
    /// A factor counts only when EVERY currency the asset is exposed to has it
    /// from a provider — a pair is no more trustworthy than its weaker side, and
    /// a differential computed from one real and one synthetic reading is not a
    /// real number.
    /// </summary>
    public static AssetProvenance Evaluate(
        Asset asset,
        ScoringProfile? profile,
        IReadOnlySet<(string Currency, string FactorCode)> backed,
        IReadOnlyDictionary<string, Factor> factors)
    {
        if (profile is null || asset.Exposures.Count == 0) return AssetProvenance.None;

        var enabled = profile.Weights.Where(w => w.IsEnabled && w.Weight > 0m).ToList();
        var totalWeight = enabled.Sum(w => w.Weight);
        if (totalWeight == 0m) return AssetProvenance.None;

        var realWeight = 0m;

        foreach (var weight in enabled)
        {
            // USD-scoped factors are defined for USD alone, so asking whether the
            // quote currency has them would fail every pair. Same split the
            // scoring contributors make — see FactorContributorBase.
            var currencies = factors.TryGetValue(weight.FactorCode, out var factor)
                             && factor.Scope == FactorScope.UsdScoped
                ? ["USD"]
                : asset.Exposures.Select(e => e.CurrencyCode).ToArray();

            if (currencies.All(c => backed.Contains((c, weight.FactorCode))))
                realWeight += weight.Weight;
        }

        return new AssetProvenance(
            realWeight == totalWeight,
            Math.Round(realWeight / totalWeight, 3));
    }
}
