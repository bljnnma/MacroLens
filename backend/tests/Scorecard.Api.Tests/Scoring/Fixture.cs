using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database.Seed;
using Scorecard.Api.Scoring;
using Scorecard.Api.Scoring.Contributors;

namespace Scorecard.Api.Tests.Scoring;

/// <summary>
/// No database, no host, no clock. The engine is exercised through exactly the
/// same projection the API uses, which is only possible because contributors are
/// pure functions of a ScoringContext (rule R1).
/// </summary>
internal static class Fixture
{
    public static readonly DateTimeOffset AsOf = new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

    public static IEnumerable<IScoreContributor> Contributors() =>
    [
        new InterestRateContributor(),
        new InflationContributor(),
        new GdpContributor(),
        new PmiContributor(),
        new EmploymentContributor(),
        new RetailSalesContributor(),
        new PositioningContributor(),
        new DollarStrengthContributor(),
        new YieldContributor()
    ];

    public static ScoringDataSet Data(DateTimeOffset? asOf = null)
    {
        var at = asOf ?? AsOf;
        var bundle = SeedData.Build(at);
        return ScoringDataLoader.Project(
            bundle.Factors, bundle.Assets, bundle.Profiles, bundle.Indicators,
            bundle.Releases, bundle.Series, bundle.Observations,
            SeedData.CurrencyPolicies(), at);
    }

    /// <summary>
    /// Data is always seeded at <see cref="AsOf"/>; only the evaluation instant
    /// moves. Re-seeding at a later date would regenerate fresh releases and make
    /// the staleness rules untestable.
    /// </summary>
    public static ScoreResult Score(string symbol, DateTimeOffset? evaluateAt = null)
    {
        var data = Data(AsOf);
        var asset = data.Assets.Single(a => a.Symbol == symbol);
        var profile = data.Profiles[asset.Market];
        var context = ScoringDataLoader.BuildContext(data, asset, profile, evaluateAt ?? AsOf);
        return new MacroScoringStrategy(Contributors()).Score(context);
    }

    public static IEnumerable<(string Symbol, ScoreResult Result)> ScoreAll(DateTimeOffset? evaluateAt = null)
    {
        var data = Data(AsOf);
        var at = evaluateAt ?? AsOf;
        var strategy = new MacroScoringStrategy(Contributors());

        foreach (var asset in data.Assets)
        {
            var profile = data.Profiles[asset.Market];
            yield return (asset.Symbol, strategy.Score(ScoringDataLoader.BuildContext(data, asset, profile, at)));
        }
    }
}
