namespace Scorecard.Api.Domain;

// Explicit values throughout: these persist as smallint, so reordering a member
// must never silently remap existing rows.

public enum Market : short
{
    Forex = 1,
    Metals = 2,
    DollarIndex = 3,
    Indices = 4,
    Crypto = 5
}

public enum Bias : short
{
    Bearish = -1,
    Neutral = 0,
    Bullish = 1
}

public enum FactorCategory : short
{
    Policy = 1,
    Inflation = 2,
    Growth = 3,
    Labour = 4,
    Sentiment = 5,
    Positioning = 6
}

/// <summary>
/// CurrencyScoped factors are defined for every currency and pairs use the
/// differential. UsdScoped factors come from US market series and are defined
/// for USD only — without this distinction the differential rule silently halves
/// them on every pair. See scoring-spec.md §3.1.
/// </summary>
public enum FactorScope : short
{
    CurrencyScoped = 1,
    UsdScoped = 2
}

public enum Impact : short
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum IndicatorUnit : short
{
    Percent = 1,
    PercentagePoints = 2,
    Thousands = 3,
    Index = 4,
    Absolute = 5
}

public enum SeriesFrequency : short
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Intraday = 4
}

/// <summary>
/// How a provider's raw series becomes the reading the engine scores.
///
/// FRED publishes levels and indices, not the derived figures traders quote.
/// The derivation happens at ingestion and is stored, never recomputed at
/// scoring time — same rule as real yield in scoring-spec.md §4.3.
/// </summary>
public enum SeriesTransform : short
{
    /// <summary>Use the published value as-is (diffusion indices).</summary>
    Level = 1,

    /// <summary>
    /// Last observation of each month, then used as a level.
    ///
    /// For daily-published series that are conceptually periodic — a policy rate
    /// is published every day but only *moves* a few times a year. Scored daily,
    /// its direction component would read zero on all but a handful of days and
    /// the "path" half of the rate signal would vanish.
    /// </summary>
    LevelMonthly = 6,

    /// <summary>Percent change against the same period a year earlier (CPI).</summary>
    YearOverYearPercent = 2,

    /// <summary>Percent change against the previous period (retail sales, GDP).</summary>
    PeriodOverPeriodPercent = 3,

    /// <summary>Absolute change against the previous period (payrolls).</summary>
    PeriodChange = 4,

    /// <summary>Mean of several series — the PMI proxy blends regional Fed surveys.</summary>
    BlendAverage = 5
}

/// <summary>Provenance for every imported value — see architecture.md §5.</summary>
public enum DataSource : short
{
    Manual = 1,
    Fred = 2,
    TradingEconomics = 3,
    Ecb = 4,
    Fed = 5,
    Cftc = 6,

    // C3b. Three providers rather than seven national agencies: BIS carries every
    // central bank's policy rate in one dataflow, and CPI splits between Eurostat
    // (euro area, Switzerland) and the OECD (UK, Australia, Canada, New Zealand)
    // only because neither one alone is current for all six.
    Bis = 7,
    Eurostat = 8,
    Oecd = 9
}
