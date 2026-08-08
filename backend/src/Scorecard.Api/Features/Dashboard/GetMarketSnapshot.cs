using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database.ReadModel;
using Scorecard.Api.Scoring;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Dashboard;

public sealed record MarketSnapshotDto(
    string StrongestCurrency,
    decimal StrongestAvg,
    string WeakestCurrency,
    decimal WeakestAvg,
    string RiskRegime,
    decimal AvgCoverage,
    int AssetCount,
    /// <summary>
    /// How many assets scored on every weighted factor. Reported next to the
    /// average so a headline 100% cannot hide a spread.
    /// </summary>
    int AssetsAtFullCoverage,
    DateTimeOffset DataAsOf);

/// <summary>
/// The dashboard's context strip. Currency strength is averaged from the same
/// per-currency scores the engine already computes, rather than being a second,
/// independently-derived number that could disagree with the heatmap.
/// </summary>
public sealed class GetMarketSnapshotHandler(
    ScoringDataLoader loader,
    LatestScoresQuery query,
    IEnumerable<IScoreContributor> allContributors)
{
    public async Task<MarketSnapshotDto?> HandleAsync(CancellationToken ct)
    {
        var asOf = DateTimeOffset.UtcNow;
        var data = await loader.LoadAsync(asOf, ct);
        var scores = await query.LatestAsync(ct);
        if (scores.Count == 0) return null;

        // Derived from the ACTIVE profiles, not a hand-written list. The list used
        // to be hardcoded and went stale the moment profile v2 retired four
        // factors: the strongest-currency figure was still averaging GDP and PMI
        // while no score used them, quietly breaking the promise in this class's
        // own summary that the number reconciles with the heatmap.
        //
        // USD-scoped factors are excluded because the probe below is a single
        // currency — a dollar-strength reading is not a property of the euro.
        var scoredFactors = data.Profiles.Values
            .SelectMany(p => p.Weights.Where(w => w.IsEnabled && w.Weight > 0m))
            .Select(w => w.FactorCode)
            .Where(code => !data.Factors.TryGetValue(code, out var f) || f.Scope == FactorScope.CurrencyScoped)
            .ToHashSet(StringComparer.Ordinal);

        var contributors = allContributors
            .Where(c => scoredFactors.Contains(c.FactorCode))
            .ToArray();

        if (contributors.Length == 0) return null;

        // Average the currency-scoped normalized scores per currency by scoring a
        // synthetic single-exposure asset — the same contributors, so the numbers
        // reconcile with what the heatmap shows.
        var averages = new Dictionary<string, decimal>();

        foreach (var currency in data.Currencies)
        {
            var probe = new Asset
            {
                Id = Guid.Empty,
                Symbol = currency,
                Market = Market.Forex,
                Name = new LocalizedText(currency, currency),
                Exposures = [new AssetCurrencyExposure { CurrencyCode = currency, Direction = 1 }]
            };

            var weights = contributors
                .Select(c => new ProfileWeight { FactorCode = c.FactorCode, Weight = 1m, Polarity = 1, IsEnabled = true })
                .ToList();

            var profile = new ScoringProfile { Market = Market.Forex, Weights = weights };
            var context = ScoringDataLoader.BuildContext(data, probe, profile, asOf);

            var values = contributors
                .Select(c => c.Evaluate(context, weights.First(w => w.FactorCode == c.FactorCode)))
                .Where(e => e is not null)
                .Select(e => (decimal)e!.Normalized)
                .ToList();

            if (values.Count > 0)
                averages[currency] = Math.Round(values.Average(), 2);
        }

        if (averages.Count == 0) return null;

        var ranked = averages
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();

        var nasdaq = scores.FirstOrDefault(s => s.Asset!.Symbol == "NASDAQ");

        return new MarketSnapshotDto(
            ranked[0].Key,
            ranked[0].Value,
            ranked[^1].Key,
            ranked[^1].Value,
            (nasdaq?.Score ?? 50m) >= 55m ? "on" : "off",
            Math.Round(scores.Average(s => s.Coverage), 3),
            scores.Count,
            scores.Count(s => s.Coverage >= 1m),
            scores.Max(s => s.DataAsOf));
    }
}

public sealed class GetMarketSnapshotEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/market-snapshot", async (
                GetMarketSnapshotHandler handler, CancellationToken ct) =>
            await handler.HandleAsync(ct) is { } snapshot
                ? Results.Ok(snapshot)
                : Results.NotFound())
            .WithName("GetMarketSnapshot")
            .WithTags("Dashboard")
            .Produces<MarketSnapshotDto>()
            .Produces(StatusCodes.Status404NotFound);
}
