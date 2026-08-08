namespace Scorecard.Api.Domain;

/// <summary>
/// What a currency's central bank is aiming at.
///
/// Configuration, not a data feed: every one of these is a published, stable
/// mandate that changes once a decade, so it belongs in the seed rather than in
/// an ingestion pipeline.
///
/// This is what lets inflation be scored the way a macro desk reads it. A
/// percentile answers "is this number high for this country"; a target gap
/// answers "does the central bank still have work to do", which is the question
/// that moves a currency.
/// </summary>
public class CurrencyPolicy
{
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// The target rate of inflation. Where a bank publishes a band rather than a
    /// point, this is the midpoint — the RBA's 2–3% becomes 2.5.
    /// </summary>
    public decimal InflationTarget { get; set; }

    /// <summary>
    /// How far inflation may sit from target before the deviation counts as
    /// policy-relevant. Half the official band where a band exists; otherwise
    /// 0.5pp, which is roughly where commentary starts calling a print an
    /// overshoot or an undershoot.
    /// </summary>
    public decimal ToleranceBand { get; set; }

    /// <summary>Central bank name, for the explanation text.</summary>
    public LocalizedText Authority { get; set; } = new();
}
