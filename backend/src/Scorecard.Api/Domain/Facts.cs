namespace Scorecard.Api.Domain;

public class IndicatorRelease
{
    public Guid Id { get; set; }
    public Guid IndicatorId { get; set; }
    public Indicator? Indicator { get; set; }

    /// <summary>
    /// Currency, not country: scoring cares about the currency a release moves.
    /// Eurozone CPI is one release for EUR, not twenty country rows.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The period the data describes, distinct from when it was published.</summary>
    public DateOnly Period { get; set; }

    /// <summary>Null until the number actually lands — the calendar entry exists first.</summary>
    public decimal? Actual { get; set; }
    public decimal? Forecast { get; set; }
    public decimal? Previous { get; set; }

    /// <summary>0 = initial print; increments on restatement.</summary>
    public int Revision { get; set; }

    public DataSource Source { get; set; } = DataSource.Manual;

    /// <summary>External id or URL — the audit trail back to the provider.</summary>
    public string? SourceRef { get; set; }

    public DateTimeOffset ReleasedAt { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}

public class SeriesObservation
{
    public Guid Id { get; set; }
    public Guid SeriesId { get; set; }
    public MarketSeries? Series { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public decimal Value { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
}
