namespace Scorecard.Api.Domain;

/// <summary>
/// The scheduler's own state for one provider-backed source.
///
/// Deliberately separate from <see cref="Indicator"/> and <see cref="MarketSeries"/>:
/// those are reference data that barely change after seeding, while this row is
/// rewritten on every poll. Mixing operational state into the reference tables
/// would mean the catalogue is dirtied dozens of times a day for reasons that
/// have nothing to do with the catalogue.
/// </summary>
public class SyncSchedule
{
    public Guid Id { get; set; }

    public SyncSourceKind SourceKind { get; set; }

    /// <summary>Indicator.Code or MarketSeries.Code.</summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Currency for indicator sources; empty for market series, which are
    /// USD-scoped by definition. Part of the natural key: after C3b one
    /// indicator has up to seven schedules, one per currency, because each
    /// national statistics office publishes on its own rhythm.
    /// </summary>
    /// <remarks>
    /// Empty rather than null on purpose. Postgres treats NULLs as DISTINCT in a
    /// unique index, so a nullable column here would let two identical market
    /// series schedules coexist — the exact duplicate the key exists to stop.
    /// </remarks>
    public string SourceCurrency { get; set; } = string.Empty;

    /// <summary>How often the provider publishes. Drives every interval below.</summary>
    public SyncCadence Cadence { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>The only ordering key the worker reads.</summary>
    public DateTimeOffset NextDueAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }

    /// <summary>
    /// Last time a poll actually wrote a row. Distinct from LastSuccessAt: a
    /// monthly series answers successfully sixty times between prints, and it is
    /// the gap since the last *change* that says whether the feed has gone quiet.
    /// </summary>
    public DateTimeOffset? LastChangeAt { get; set; }

    public int ConsecutiveFailures { get; set; }

    public string? LastError { get; set; }
}

public enum SyncSourceKind : short
{
    Series = 1,
    Indicator = 2
}

/// <summary>
/// Publication rhythm, not polling frequency — see <c>SyncCadencePolicy</c> for
/// the mapping. The DXY entry is why this enum exists at all: DTWEXBGS carries
/// daily observations but ships weekly, and treating the two as the same number
/// is what collapsed coverage in C3a.
/// </summary>
public enum SyncCadence : short
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4
}
