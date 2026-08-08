namespace Scorecard.Api.Domain;

/// <summary>
/// A scoring dimension: one heatmap column, one detail-page row, one weight key.
/// Replaces the earlier "indicator code" keying so that series-derived factors
/// (DXY, YIELD) and release-derived factors can share one namespace.
/// </summary>
public class Factor
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public LocalizedText ShortName { get; set; } = new();
    public LocalizedText Description { get; set; } = new();
    public FactorCategory Category { get; set; }
    public FactorScope Scope { get; set; }
    public int DisplayOrder { get; set; }
}

public class Asset
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public Market Market { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public List<AssetCurrencyExposure> Exposures { get; set; } = [];
}

/// <summary>
/// What makes the engine generic: without this table every new pair would be a
/// code change. For indices the row means "which economy drives this asset",
/// not "quote currency" — the profile's polarity carries the sign.
/// </summary>
public class AssetCurrencyExposure
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>+1 = base side, -1 = quote side.</summary>
    public short Direction { get; set; }
}

public class Indicator
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>Which factor this indicator feeds. Several may feed one factor.</summary>
    public string FactorCode { get; set; } = string.Empty;

    public LocalizedText Name { get; set; } = new();
    public LocalizedText Description { get; set; } = new();
    public LocalizedText WhyItMatters { get; set; } = new();
    public LocalizedText HowItAffects { get; set; } = new();
    public FactorCategory Category { get; set; }

    /// <summary>+1 = a higher reading strengthens the currency; -1 = weakens it.</summary>
    public short CurrencyDirection { get; set; } = 1;

    public Impact Impact { get; set; } = Impact.Medium;
    public IndicatorUnit Unit { get; set; }

    /// <summary>Surprise threshold for ±1, in the indicator's native units.</summary>
    public decimal BandMinor { get; set; }

    /// <summary>Surprise threshold for ±2.</summary>
    public decimal BandMajor { get; set; }

    /// <summary>Past this age a release is unavailable, not merely stale.</summary>
    public int MaxAgeDays { get; set; } = 60;

    /// <summary>
    /// Provider series backing this indicator for USD. Comma-separated for blends.
    /// </summary>
    /// <remarks>
    /// Superseded by <see cref="Sources"/> in C3b and no longer read by ingestion.
    /// Kept so historical rows stamped with it stay interpretable; new mappings
    /// belong on <see cref="IndicatorSource"/>, which can express one indicator
    /// backed by a different provider per currency.
    /// </remarks>
    public string? ProviderSeriesId { get; set; }

    public SeriesTransform Transform { get; set; } = SeriesTransform.Level;

    /// <summary>Per-currency provider mappings. Empty means maintained by hand.</summary>
    public List<IndicatorSource> Sources { get; set; } = [];

    /// <summary>
    /// Set when the reading is a stand-in rather than the named statistic — the
    /// PMI proxy blends regional Fed surveys because ISM is licensed and cannot
    /// be redistributed. Surfaced in the UI so the substitution is never silent.
    /// </summary>
    public bool IsProxy { get; set; }
}

/// <summary>
/// Which provider backs one indicator for one currency.
///
/// A row rather than a column on <see cref="Indicator"/> because the answer is
/// per-currency: CPI comes from Eurostat for EUR and CHF, from the OECD for GBP,
/// AUD, CAD and NZD, and from FRED for USD — one indicator, four providers. The
/// USD-only column this replaces could not express that.
/// </summary>
public class IndicatorSource
{
    public Guid Id { get; set; }
    public Guid IndicatorId { get; set; }
    public Indicator? Indicator { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DataSource Provider { get; set; }

    /// <summary>
    /// The provider's own key, in whatever shape that provider needs. Opaque
    /// here on purpose — each client documents its encoding, and forcing one
    /// scheme on four APIs would mean parsing in the wrong place.
    /// </summary>
    public string ProviderSeriesId { get; set; } = string.Empty;

    public SeriesTransform Transform { get; set; } = SeriesTransform.Level;

    /// <summary>Publication rhythm — drives the scheduler, not the scoring.</summary>
    public SyncCadence Cadence { get; set; } = SyncCadence.Monthly;

    public bool IsEnabled { get; set; } = true;
}

public class MarketSeries
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FactorCode { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public LocalizedText Description { get; set; } = new();
    public IndicatorUnit Unit { get; set; }
    public SeriesFrequency Frequency { get; set; } = SeriesFrequency.Daily;
    public DataSource Source { get; set; } = DataSource.Manual;
    public int MaxAgeDays { get; set; } = 5;

    /// <summary>
    /// The provider's identifier for this series (e.g. FRED's DFII10). Null
    /// means the series is maintained by hand. Kept here rather than in config
    /// because it is provenance, and provenance lives with the row.
    /// </summary>
    public string? ProviderSeriesId { get; set; }

    /// <summary>Publication rhythm — see IndicatorSource.Cadence.</summary>
    public SyncCadence Cadence { get; set; } = SyncCadence.Daily;

    /// <summary>
    /// Short qualifier shown beside the raw value, for series whose number is
    /// meaningless without knowing which index it is.
    ///
    /// The dollar is the case that forced it: this project reads the Fed's broad
    /// trade-weighted index, which sits near 120 on a 2006 = 100 base, while the
    /// DXY a trader has on screen is the ICE index near 99. Percentile scoring is
    /// scale-invariant so the SCORE is unaffected, but a bare "119.70" under a
    /// dollar heading reads as wrong data.
    ///
    /// Empty where the unit speaks for itself — a real yield in percent needs no
    /// qualifier.
    /// </summary>
    public LocalizedText ScaleNote { get; set; } = new();

    public List<SeriesObservation> Observations { get; set; } = [];
}
