namespace Scorecard.Api.Domain;

/// <summary>
/// Append-only and versioned. Once a profile has produced an AssetScore it is
/// frozen: tuning creates version N+1 and deactivates N. Editing a used profile
/// would silently rewrite history and make engine_version a lie (rule R3).
/// </summary>
public class ScoringProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public LocalizedText Description { get; set; } = new();
    public int Version { get; set; } = 1;
    public Market Market { get; set; }
    public bool IsActive { get; set; }

    public decimal BullishThreshold { get; set; } = 65m;
    public decimal BearishThreshold { get; set; } = 35m;

    /// <summary>Below this coverage a score is stored but excluded from ranking.</summary>
    public decimal MinCoverage { get; set; } = 0.60m;

    public DateTimeOffset CreatedAt { get; set; }

    public List<ProfileWeight> Weights { get; set; } = [];
}

/// <summary>
/// The weight rows ARE the contributor selection: a profile with no enabled row
/// for a factor simply does not run that contributor. One configuration surface,
/// so selection and weighting cannot drift apart.
/// </summary>
public class ProfileWeight
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public ScoringProfile? Profile { get; set; }
    public string FactorCode { get; set; } = string.Empty;
    public decimal Weight { get; set; }

    /// <summary>
    /// +1 or -1. Equity indices respond inversely to USD-bullish data on some
    /// factors, which no pure currency mapping can express. See scoring-spec.md §5.2.
    /// </summary>
    public short Polarity { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;
}

/// <summary>Append-only: historical scores are never mutated (rule R2).</summary>
public class AssetScore
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public decimal Score { get; set; }
    public Bias Bias { get; set; }

    /// <summary>Participating weight over total enabled weight, 0..1.</summary>
    public decimal Coverage { get; set; }

    public bool IsSufficient { get; set; }

    public Guid ScoringProfileId { get; set; }
    public ScoringProfile? ScoringProfile { get; set; }

    /// <summary>Denormalised snapshot so the score stays readable if the profile moves on.</summary>
    public int ProfileVersion { get; set; }

    public string EngineVersion { get; set; } = string.Empty;

    /// <summary>Newest input included — distinct from CalculatedAt.</summary>
    public DateTimeOffset DataAsOf { get; set; }

    public DateTimeOffset CalculatedAt { get; set; }
    public int CalculationDurationMs { get; set; }

    public List<AssetFactorScore> Factors { get; set; } = [];
}

/// <summary>Append-only audit record of one factor's contribution to one score.</summary>
public class AssetFactorScore
{
    public Guid Id { get; set; }
    public Guid AssetScoreId { get; set; }
    public AssetScore? AssetScore { get; set; }

    /// <summary>
    /// Denormalised on purpose, not an FK: renaming or retiring a factor must
    /// never alter a historical explanation (rule R4).
    /// </summary>
    public string FactorCode { get; set; } = string.Empty;

    /// <summary>What the data actually said, snapshotted.</summary>
    public decimal? RawValue { get; set; }

    public string RawLabelMn { get; set; } = string.Empty;
    public string RawLabelEn { get; set; } = string.Empty;

    /// <summary>
    /// -2..+2 in half steps, or null when the factor was unavailable. This is the
    /// heatmap cell. Half steps because a pair's score is a differential —
    /// see <c>Rounding.ToNormalized</c>.
    /// </summary>
    public decimal? NormalizedScore { get; set; }

    public decimal Weight { get; set; }
    public short Polarity { get; set; } = 1;

    /// <summary>Weighted score points. Sums with the base 50 to the final score.</summary>
    public decimal Contribution { get; set; }

    // Flat columns, not jsonb: engine-generated, high volume, never expands
    // beyond the supported locales.
    public string ExplanationMn { get; set; } = string.Empty;
    public string ExplanationEn { get; set; } = string.Empty;

    /// <summary>
    /// The per-currency readings the score was built from — one entry for a
    /// USD-scoped factor, two for a pair.
    ///
    /// Without this a pair cell is unexplainable: it shows the base currency's
    /// raw value next to a score computed from BOTH sides, and a reader has no
    /// way to reconcile "6.3%" with "-1". Storing it rather than recomputing
    /// keeps rule R4 — the reading is part of what the score was based on.
    ///
    /// jsonb, unlike the explanation columns above: cardinality varies with the
    /// asset, nothing queries inside it, and it is always read with its parent
    /// row.
    /// </summary>
    public List<FactorReading> Readings { get; set; } = [];
}

/// <summary>One currency's contribution to a factor, before the differential.</summary>
public sealed record FactorReading(
    string Currency,

    /// <summary>+1 base side, -1 quote side.</summary>
    short Direction,

    /// <summary>This side's own -2..+2, before the pair difference and polarity.</summary>
    short Normalized,

    decimal? RawValue,
    string LabelMn,
    string LabelEn);
