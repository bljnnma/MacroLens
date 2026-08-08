using Scorecard.Api.Domain;
using Scorecard.Api.Scoring.Normalization;

namespace Scorecard.Api.Scoring.Contributors;

/// <summary>
/// Owns the one piece of logic every factor shares: mapping a per-currency score
/// onto an asset through its exposures. Written once here rather than nine times
/// in the contributors, which is what keeps each contributor a few lines.
/// </summary>
public abstract class FactorContributorBase : IScoreContributor
{
    public abstract string FactorCode { get; }

    /// <summary>Per-currency normalized score, or null when unusable.</summary>
    protected abstract CurrencyReading? Read(ScoringContext context, string currency);

    public FactorEvaluation? Evaluate(ScoringContext context, ProfileWeight weight)
    {
        if (!context.Factors.TryGetValue(FactorCode, out var factor)) return null;

        var details = new List<ReadingDetail>();
        decimal s;

        if (factor.Scope == FactorScope.UsdScoped)
        {
            // USD-scoped factors bypass the differential entirely. Halving them
            // on a pair — which (0 - c_USD)/2 would do — is the bug this exists
            // to prevent. See scoring-spec.md §3.1.
            var usd = context.Asset.Exposures.FirstOrDefault(e => e.CurrencyCode == "USD");
            if (usd is null) return null;

            var reading = Read(context, "USD");
            if (reading is null) return null;

            details.Add(new ReadingDetail("USD", usd.Direction, reading));
            s = usd.Direction * reading.Score;
        }
        else
        {
            foreach (var exposure in context.Asset.Exposures.OrderByDescending(e => e.Direction))
            {
                var reading = Read(context, exposure.CurrencyCode);
                if (reading is null) return null;
                details.Add(new ReadingDetail(exposure.CurrencyCode, exposure.Direction, reading));
            }

            if (details.Count == 2)
            {
                var b = details.First(d => d.Direction == 1);
                var q = details.First(d => d.Direction == -1);
                // /2 keeps the result inside -2..+2: the difference spans -4..+4.
                s = (b.Reading.Score - q.Reading.Score) / 2m;
            }
            else if (details.Count == 1)
            {
                s = details[0].Direction * details[0].Reading.Score;
            }
            else
            {
                return null;
            }
        }

        var normalized = Rounding.ToNormalized(weight.Polarity * s);
        var primary = details[0].Reading;

        return new FactorEvaluation(
            FactorCode,
            normalized,
            primary.RawValue,
            primary.LabelMn,
            primary.LabelEn,
            details);
    }
}

/// <summary>Factors driven by economic releases and surprise normalization.</summary>
public abstract class ReleaseContributor : FactorContributorBase
{
    protected override CurrencyReading? Read(ScoringContext context, string currency)
    {
        var snapshot = context.Release(FactorCode, currency);
        if (snapshot is null) return null;

        // Stale data is worse than missing data — it looks authoritative.
        var age = context.AsOfUtc - snapshot.Release.ReleasedAt;
        if (age.TotalDays > snapshot.Indicator.MaxAgeDays) return null;

        if (snapshot.Release.Actual is not { } actual) return null;

        var score = LevelTrendNormalizer.Evaluate(
            actual,
            snapshot.Release.Previous,
            snapshot.History,
            snapshot.Indicator.CurrencyDirection,
            out var levelUsed);

        // Disclosed rather than hidden: with a short history the level component
        // is absent and only direction contributed, so the factor can only reach
        // +/-1. The label says so.
        var (mn, en) = Labels.Release(snapshot, levelUsed);
        return new CurrencyReading(score, actual, mn, en);
    }
}

/// <summary>Factors derived from US market series and percentile ranking.</summary>
public abstract class SeriesContributor : FactorContributorBase
{
    protected override CurrencyReading? Read(ScoringContext context, string currency)
    {
        if (currency != "USD") return null;

        var snapshot = context.SeriesFor(FactorCode);
        if (snapshot is null || snapshot.Window.Count == 0) return null;

        var newest = snapshot.Window[^1];
        var age = context.AsOfUtc - newest.ObservedAt;
        if (age.TotalDays > snapshot.Series.MaxAgeDays) return null;

        var score = PercentileNormalizer.Evaluate(snapshot.Window);
        if (score is null) return null;

        var percentile = PercentileNormalizer.Percentile(snapshot.Window);
        var (mn, en) = Labels.Series(newest.Value, percentile, snapshot.Series);
        return new CurrencyReading(score.Value, newest.Value, mn, en);
    }
}

public sealed class InterestRateContributor : FactorContributorBase
{
    public override string FactorCode => FactorCodes.Rate;

    protected override CurrencyReading? Read(ScoringContext context, string currency)
    {
        var snapshot = context.Release(FactorCode, currency);
        if (snapshot is null) return null;

        var age = context.AsOfUtc - snapshot.Release.ReleasedAt;
        if (age.TotalDays > snapshot.Indicator.MaxAgeDays) return null;
        if (snapshot.Release.Actual is not { } actual) return null;

        // The whole universe is needed, not just this asset's currencies: the
        // level component is a cross-sectional rank.
        var universe = new Dictionary<string, decimal>();
        foreach (var code in context.Currencies)
        {
            var other = context.Release(FactorCode, code);
            if (other?.Release.Actual is { } rate) universe[code] = rate;
        }

        var score = TrendNormalizer.Evaluate(actual, snapshot.Release.Previous, currency, universe);
        var (mn, en) = Labels.PolicyRate(snapshot);
        return new CurrencyReading(score, actual, mn, en);
    }
}

/// <summary>
/// Inflation scored against the central bank's mandate rather than its own past.
///
/// Does not inherit ReleaseContributor: the base class scores level-and-direction
/// within the indicator's own history, which is exactly the definition v2.1.0
/// replaces here.
/// </summary>
public sealed class InflationContributor : FactorContributorBase
{
    public override string FactorCode => FactorCodes.Cpi;

    protected override CurrencyReading? Read(ScoringContext context, string currency)
    {
        var snapshot = context.Release(FactorCode, currency);
        if (snapshot is null) return null;

        var age = context.AsOfUtc - snapshot.Release.ReleasedAt;
        if (age.TotalDays > snapshot.Indicator.MaxAgeDays) return null;
        if (snapshot.Release.Actual is not { } actual) return null;

        // Without a published mandate there is no gap to measure. Falling back to
        // the percentile would mean one currency scored on a different definition
        // than the rest, which is worse than the factor being unavailable.
        if (!context.Policies.TryGetValue(currency, out var policy)) return null;

        var score = TargetGapNormalizer.Evaluate(
            actual, snapshot.Release.Previous, policy.InflationTarget, policy.ToleranceBand);

        // CurrencyDirection is not applied: the target gap already carries the
        // sign, and multiplying by it again would invert every reading.
        var (mn, en) = Labels.InflationTarget(actual, snapshot.Release.Previous, policy);
        return new CurrencyReading(score, actual, mn, en);
    }
}

public sealed class GdpContributor : ReleaseContributor
{
    public override string FactorCode => FactorCodes.Gdp;
}

public sealed class PmiContributor : ReleaseContributor
{
    public override string FactorCode => FactorCodes.Pmi;
}

public sealed class EmploymentContributor : ReleaseContributor
{
    public override string FactorCode => FactorCodes.Labour;
}

public sealed class RetailSalesContributor : ReleaseContributor
{
    public override string FactorCode => FactorCodes.Retail;
}

public sealed class PositioningContributor : ReleaseContributor
{
    public override string FactorCode => FactorCodes.Cot;
}

public sealed class DollarStrengthContributor : SeriesContributor
{
    public override string FactorCode => FactorCodes.Dxy;
}

public sealed class YieldContributor : SeriesContributor
{
    public override string FactorCode => FactorCodes.Yield;
}

public static class FactorCodes
{
    public const string Rate = "RATE";
    public const string Cpi = "CPI";
    public const string Gdp = "GDP";
    public const string Pmi = "PMI";
    /// <summary>
    /// Superseded by <see cref="Labour"/> in profile v2. The code lives on
    /// because historical AssetFactorScore rows are stamped with it and rule R2
    /// forbids rewriting them — a retired code is inert, never deleted.
    /// </summary>
    public const string Nfp = "NFP";

    public const string Labour = "LABOUR";
    public const string Retail = "RETAIL";
    public const string Dxy = "DXY";
    public const string Yield = "YIELD";
    public const string Cot = "COT";
}
