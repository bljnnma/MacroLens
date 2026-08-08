using Scorecard.Api.Domain;

namespace Scorecard.Api.Scoring;

public static class EngineVersion
{
    /// <summary>
    /// Stamped on every AssetScore. Any change to normalization thresholds,
    /// aggregation or explanation templates requires a bump — that is what keeps
    /// historical scores reproducible after the model moves on.
    /// </summary>
    /// <summary>
    /// 2.0.0 — release factors moved from surprise-vs-consensus to
    /// level-plus-direction. A major bump because it redefines what every score
    /// means, not merely how one is computed. v1.0.0 scores remain reproducible
    /// from their stored factor rows.
    /// </summary>
    /// <summary>
    /// 2.2.0 — pair factors carry half steps. A differential is (base - quote)/2
    /// and is therefore a half-integer; rounding it to a whole number discarded
    /// information in about half of all pair cells and, rounding away from zero,
    /// pushed every score away from neutral. Not a redefinition of any factor,
    /// so a minor bump — but every pair score moves.
    ///
    /// 2.1.0 — inflation moved from an own-history percentile to a gap against
    /// the central bank's published target.
    /// </summary>
    public const string Current = "2.2.0";

    public const decimal BaseScore = 50m;
}

/// <summary>
/// The latest usable release for one factor in one currency, plus the printed
/// readings behind it. v2.0.0 needs the history: a level component is a
/// percentile within the indicator's own past, so a single point is not enough.
/// </summary>
public sealed record ReleaseSnapshot(
    Indicator Indicator,
    IndicatorRelease Release,
    IReadOnlyList<decimal> History);

/// <summary>A series plus its trailing observation window, newest last.</summary>
public sealed record SeriesSnapshot(MarketSeries Series, IReadOnlyList<SeriesObservation> Window);

/// <summary>Normalized value of one factor for one currency, with display text.</summary>
public sealed record CurrencyReading(short Score, decimal? RawValue, string LabelMn, string LabelEn);

public sealed record ReadingDetail(string Currency, short Direction, CurrencyReading Reading);

/// <summary>What a contributor returns before weighting — the strategy owns the maths.</summary>
public sealed record FactorEvaluation(
    string FactorCode,
    /// <summary>
    /// -2..+2 in half steps. Whole for a single-currency factor, half-integer for
    /// a pair — see <c>Rounding.ToNormalized</c>.
    /// </summary>
    decimal Normalized,
    decimal? RawValue,
    string RawLabelMn,
    string RawLabelEn,
    IReadOnlyList<ReadingDetail> Readings);

/// <summary>
/// Everything a scoring run needs, loaded exactly once before evaluation.
///
/// This type is why contributors can be pure: no DbContext, no clock, no I/O
/// reaches them, so the same context always produces the same score (rule R1).
/// </summary>
public sealed class ScoringContext
{
    public required Asset Asset { get; init; }
    public required ScoringProfile Profile { get; init; }
    public required IReadOnlyDictionary<string, Factor> Factors { get; init; }

    /// <summary>Keyed by (factor code, currency).</summary>
    public required IReadOnlyDictionary<(string FactorCode, string Currency), ReleaseSnapshot> Releases { get; init; }

    /// <summary>Keyed by factor code — USD-scoped series only.</summary>
    public required IReadOnlyDictionary<string, SeriesSnapshot> Series { get; init; }

    /// <summary>The full currency universe, needed for cross-sectional ranking.</summary>
    public required IReadOnlyList<string> Currencies { get; init; }

    /// <summary>
    /// Central bank mandates by currency. Loaded here rather than read by the
    /// contributor for the same reason as everything else in this type: rule R1
    /// says a contributor receives data, never a way to fetch it.
    /// </summary>
    public required IReadOnlyDictionary<string, CurrencyPolicy> Policies { get; init; }

    /// <summary>
    /// Age is measured against this, never DateTimeOffset.UtcNow — otherwise a
    /// re-run of a historical context would produce different staleness results.
    /// </summary>
    public required DateTimeOffset AsOfUtc { get; init; }

    public ReleaseSnapshot? Release(string factorCode, string currency) =>
        Releases.TryGetValue((factorCode, currency), out var r) ? r : null;

    public SeriesSnapshot? SeriesFor(string factorCode) =>
        Series.TryGetValue(factorCode, out var s) ? s : null;
}

public interface IScoreContributor
{
    string FactorCode { get; }

    /// <summary>Null when the factor cannot be evaluated — that reduces coverage.</summary>
    FactorEvaluation? Evaluate(ScoringContext context, ProfileWeight weight);
}

public interface IScoringStrategy
{
    /// <summary>
    /// MacroScoringStrategy answers true for everything. The seam exists for a
    /// market that genuinely aggregates differently later — Crypto may need
    /// on-chain inputs that do not fit the release/series model — rather than
    /// for four near-identical classes today.
    /// </summary>
    bool Handles(Market market);

    ScoreResult Score(ScoringContext context);
}

public sealed record ScoredFactor(
    string FactorCode,
    /// <summary>-2..+2 in half steps, or null when the factor was unavailable.</summary>
    decimal? Normalized,
    decimal? RawValue,
    string RawLabelMn,
    string RawLabelEn,
    decimal Weight,
    short Polarity,
    decimal Contribution,
    string ExplanationMn,
    string ExplanationEn,
    /// <summary>
    /// Per-currency readings behind the score. Empty when the factor was
    /// unavailable — there was nothing to read.
    /// </summary>
    IReadOnlyList<FactorReading> Readings);

public sealed record ScoreResult(
    decimal Score,
    Bias Bias,
    decimal Coverage,
    bool IsSufficient,
    IReadOnlyList<ScoredFactor> Factors);
