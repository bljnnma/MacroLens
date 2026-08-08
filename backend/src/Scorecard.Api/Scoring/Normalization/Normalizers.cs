using Scorecard.Api.Domain;

namespace Scorecard.Api.Scoring.Normalization;

public static class Rounding
{
    /// <summary>
    /// Clamps a factor score to -2..+2 at HALF-step resolution.
    ///
    /// Half steps are the arithmetic's own resolution, not invented precision.
    /// Each currency's reading is an integer by construction (level plus
    /// direction), so a pair's differential — (base - quote) / 2 — is exactly a
    /// half-integer. Rounding that to a whole number threw away real information
    /// in 48% of pair cells, and because the rounding went away from zero it did
    /// so with a systematic bias: every 0.5 became 1.0 and every 1.5 became 2.0,
    /// inflating scores away from the neutral 50. On live data that moved USDCHF
    /// by 9.8 points and was most of the reason it was the only bullish call in
    /// the universe.
    ///
    /// Rounding away from zero is kept for the residual, so a value that is not
    /// already a half-step resolves the same way on every platform — .NET's
    /// banker's rounding would send 0.25 to 0.0.
    /// </summary>
    public static decimal ToNormalized(decimal value) =>
        Math.Clamp(Math.Round(value * 2m, MidpointRounding.AwayFromZero) / 2m, -2m, 2m);

    /// <summary>
    /// Contributions carry one decimal, and the score is the sum of the ROUNDED
    /// contributions. The arithmetic a user checks by hand must be the arithmetic
    /// the engine did — so 50 + Σ closes exactly on screen.
    /// </summary>
    public static decimal ToContribution(decimal value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Engine v2.0.0's normalizer for release-derived factors.
///
/// Replaces surprise-vs-consensus, which FRED cannot support — it publishes time
/// series, not calendar events with a survey consensus. Scoring a series as a
/// series is the honest fit, and it matches what the product claims to measure:
/// the standing macro backdrop, not repricing at the moment of release.
///
/// Deliberately the same shape as <see cref="TrendNormalizer"/>, so every factor
/// in the engine now reads as "level, plus which way it is moving".
/// </summary>
public static class LevelTrendNormalizer
{
    /// <summary>Below this many readings the level component is not trustworthy.</summary>
    public const int MinimumHistory = 12;

    /// <param name="history">Printed readings, oldest first, including the current one.</param>
    /// <param name="levelUsed">False when history was too short and only direction contributed.</param>
    public static short Evaluate(
        decimal current,
        decimal? previous,
        IReadOnlyList<decimal> history,
        short currencyDirection,
        out bool levelUsed)
    {
        levelUsed = false;

        // Prefer the release's own Previous field; fall back to the prior reading
        // in history. Seeded fixtures carry the former, FRED gives the latter.
        var prior = previous ?? (history.Count >= 2 ? history[^2] : (decimal?)null);
        var direction = prior is { } p ? Math.Sign(current - p) : 0;

        var level = 0;
        if (history.Count >= MinimumHistory)
        {
            levelUsed = true;
            var below = history.Count(v => v < current);
            var percentile = (decimal)below / history.Count;

            // Tighter buckets than the series percentile: combined with direction
            // this spans exactly -2..+2 with no clamping loss.
            level = percentile >= 0.70m ? 1 : percentile <= 0.30m ? -1 : 0;
        }

        return (short)Math.Clamp((level + direction) * currencyDirection, -2, 2);
    }
}

/// <summary>
/// Engine v2.1.0's normalizer for inflation.
///
/// Replaces the own-history percentile, which answers the wrong question. A
/// percentile says "3.5% is high for this country"; a target gap says "the
/// central bank is 1.5pp from its mandate and still has work to do", and it is
/// the second question that moves a currency. It is also more objective, not
/// less: every target here is a published mandate rather than a rolling
/// statistic that shifts as history accumulates.
///
/// Sign convention: above target is POSITIVE for the currency, because the bank
/// is pushed toward tightening. Below target is negative for the same reason in
/// reverse.
/// </summary>
public static class TargetGapNormalizer
{
    /// <param name="current">Latest inflation reading.</param>
    /// <param name="previous">Prior reading, for the convergence direction.</param>
    /// <param name="target">The bank's published objective.</param>
    /// <param name="tolerance">How far off target still counts as on target.</param>
    public static short Evaluate(decimal current, decimal? previous, decimal target, decimal tolerance)
    {
        var gap = current - target;

        var level = gap > tolerance ? 1
            : gap < -tolerance ? -1
            : 0;

        // Direction is measured on the GAP, not on the level. Inflation rising
        // from 1.0 to 1.5 against a 2% target is the gap CLOSING, which is
        // disinflationary pressure easing — the opposite of inflation rising
        // from 2.5 to 3.0. Scoring the raw change would call both hawkish.
        var direction = 0;
        if (previous is { } prior)
        {
            var priorGap = prior - target;
            var widened = Math.Abs(gap) - Math.Abs(priorGap);

            if (widened != 0m)
                // Widening reinforces whichever side of target we are on;
                // narrowing pulls toward neutral.
                direction = Math.Sign(widened) * (gap >= 0 ? 1 : -1);
        }

        return (short)Math.Clamp(level + direction, -2, 2);
    }

    /// <summary>
    /// Real policy rate — nominal minus inflation. Shown as context rather than
    /// scored: with the policy rate and inflation both weighted, the real rate is
    /// a linear combination of the two and adds no independent information to the
    /// aggregate. It is still the number a macro desk quotes, so it is displayed.
    /// </summary>
    public static decimal? RealRate(decimal? policyRate, decimal? inflation) =>
        policyRate is { } r && inflation is { } i ? r - i : null;
}

/// <summary>
/// Engine v1.0.0 only. Retained so a v1 score can still be explained, but no
/// contributor calls it — see LevelTrendNormalizer for the current definition.
/// </summary>
public static class SurpriseNormalizer
{
    /// <summary>
    /// Fixed per-indicator bands rather than rolling dispersion: with seeded data
    /// the history is thin, and a rolling statistic would shift historical scores
    /// for reasons unrelated to the model.
    /// </summary>
    public static short? Evaluate(
        IndicatorRelease release,
        Indicator indicator,
        out bool usedFallback)
    {
        usedFallback = false;
        if (release.Actual is not { } actual) return null;

        // Falling back to `previous` raises coverage and is disclosed in the
        // explanation, which beats discarding a real signal silently.
        decimal? reference = release.Forecast;
        if (reference is null)
        {
            reference = release.Previous;
            usedFallback = true;
        }
        if (reference is not { } baseline) return null;

        var d = actual - baseline;
        var magnitude = Math.Abs(d) < indicator.BandMinor
            ? 0
            : Math.Abs(d) < indicator.BandMajor
                ? 1
                : 2;

        var sign = Math.Sign(d);
        return (short)Math.Clamp(sign * magnitude * indicator.CurrencyDirection, -2, 2);
    }
}

public static class TrendNormalizer
{
    /// <summary>
    /// Policy rates are a level and a path, not a surprise. Direction captures the
    /// path; a cross-sectional rank across the whole currency universe captures
    /// the differential, which is the dominant driver of FX. This is the one place
    /// the engine looks sideways at other currencies.
    /// </summary>
    public static short Evaluate(
        decimal actual,
        decimal? previous,
        string currency,
        IReadOnlyDictionary<string, decimal> universeRates)
    {
        var direction = previous is { } p ? Math.Sign(actual - p) : 0;

        // Ties resolve by currency code ascending so the ranking is deterministic.
        var ranked = universeRates
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key)
            .ToList();

        var index = ranked.IndexOf(currency);
        var level = 0;
        if (index >= 0 && ranked.Count >= 4)
        {
            if (index < 2) level = 1;
            else if (index >= ranked.Count - 2) level = -1;
        }

        return (short)Math.Clamp(direction + level, -2, 2);
    }
}

public static class PercentileNormalizer
{
    public const int MinimumWindow = 60;

    /// <summary>
    /// Percentile rank of the newest observation within its trailing window.
    /// Below <see cref="MinimumWindow"/> observations the factor is unavailable
    /// rather than computed on thin data.
    /// </summary>
    public static short? Evaluate(IReadOnlyList<SeriesObservation> window)
    {
        if (window.Count < MinimumWindow) return null;

        var latest = window[^1].Value;
        var below = window.Count(o => o.Value < latest);
        var p = (decimal)below / window.Count;

        return p switch
        {
            >= 0.80m => (short)2,
            >= 0.60m => (short)1,
            >= 0.40m => (short)0,
            >= 0.20m => (short)-1,
            _ => (short)-2
        };
    }

    public static decimal Percentile(IReadOnlyList<SeriesObservation> window)
    {
        if (window.Count == 0) return 0m;
        var latest = window[^1].Value;
        return (decimal)window.Count(o => o.Value < latest) / window.Count;
    }
}
