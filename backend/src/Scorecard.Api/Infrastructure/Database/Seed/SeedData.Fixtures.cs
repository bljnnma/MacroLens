using Scorecard.Api.Domain;
using Scorecard.Api.Scoring.Contributors;

namespace Scorecard.Api.Infrastructure.Database.Seed;

public static partial class SeedData
{
    /// <summary>
    /// The version every market's active profile should be on. Bumping this and
    /// editing <see cref="Profiles"/> ships a new profile through reconciliation;
    /// the previous version is deactivated, never edited (rule R3).
    /// </summary>
    public const int ProfileVersion = 2;

    /// <summary>
    /// Profile v2 — five weighted factors.
    ///
    /// v1 carried nine. Four were retired because they could not be scored
    /// honestly rather than because they do not matter:
    /// <list type="bullet">
    /// <item><b>GDP</b> — quarterly, so it can never reach the 12 readings the
    /// level component needs inside the load window; direction-only, capped at
    /// ±1, and three months stale by construction.</item>
    /// <item><b>PMI</b> — ISM and S&amp;P Global are licensed. The USD figure was
    /// a regional Fed proxy on a different scale, and no free equivalent exists
    /// for the other six currencies.</item>
    /// <item><b>RETAIL</b> — never sourced beyond USD; a factor present on one
    /// side of a pair cannot form a differential.</item>
    /// <item><b>COT</b> — weight was already zero, so the row only added noise.</item>
    /// </list>
    ///
    /// Real policy rate (nominal minus inflation) is deliberately NOT a factor.
    /// With RATE and CPI both weighted it is a linear combination of the two and
    /// adds no independent information to the aggregate — it would only count
    /// inflation twice with opposite signs. It is surfaced as context instead.
    /// </summary>
    public static List<ScoringProfile> Profiles(DateTimeOffset asOf) =>
    [
        // Weights keep v1's relative judgement, renormalised from 74 to 100.
        P(Market.Forex, "Forex Core", asOf,
            (FactorCodes.Rate, 34m, 1), (FactorCodes.Cpi, 24m, 1), (FactorCodes.Labour, 21m, 1),
            (FactorCodes.Dxy, 13m, 1), (FactorCodes.Yield, 8m, 1)),

        // Gold has no labour row, as in v1: it is not scored against another
        // economy, so a domestic employment reading has no counterpart.
        P(Market.Metals, "Metals Core", asOf,
            (FactorCodes.Yield, 30m, 1), (FactorCodes.Dxy, 30m, 1), (FactorCodes.Rate, 26m, 1),
            (FactorCodes.Cpi, 14m, 1)),

        // No DXY row: scoring the dollar index by the dollar index is circular.
        // RATE is moderated from a straight renormalisation (45) to 40 — with
        // only four factors, a score that is nearly half one input stops being a
        // multi-factor reading.
        P(Market.DollarIndex, "Dollar Index Core", asOf,
            (FactorCodes.Rate, 40m, 1), (FactorCodes.Cpi, 25m, 1), (FactorCodes.Labour, 25m, 1),
            (FactorCodes.Yield, 10m, 1)),

        // Polarity earns its keep here: tighter policy and higher real yields are
        // bearish for equities, which no pure currency mapping can express.
        // Labour stays positive — a strong economy supports earnings.
        P(Market.Indices, "Index Core", asOf,
            (FactorCodes.Yield, 35m, -1), (FactorCodes.Rate, 34m, -1), (FactorCodes.Cpi, 14m, -1),
            (FactorCodes.Labour, 9m, 1), (FactorCodes.Dxy, 8m, -1))
    ];

    private static ScoringProfile P(Market market, string name, DateTimeOffset asOf,
        params (string Factor, decimal Weight, short Polarity)[] weights)
    {
        var profile = new ScoringProfile
        {
            Id = Id($"profile:{market}:{ProfileVersion}"),
            Name = name,
            Market = market,
            Version = ProfileVersion,
            IsActive = true,
            BullishThreshold = 65m,
            BearishThreshold = 35m,
            MinCoverage = 0.60m,
            CreatedAt = asOf,
            Description = L($"{name} v1", $"{name} v1")
        };

        foreach (var (factor, weight, polarity) in weights)
        {
            profile.Weights.Add(new ProfileWeight
            {
                Id = Id($"weight:{market}:{ProfileVersion}:{factor}"),
                ProfileId = profile.Id,
                FactorCode = factor,
                Weight = weight,
                Polarity = polarity,
                IsEnabled = true
            });
        }

        return profile;
    }

    /// <summary>(currency, actual, forecast, previous) — null actual means "not yet printed".</summary>
    private static readonly (string Code, (string Cur, decimal? A, decimal? F, decimal? P)[] Rows)[] ReleaseTable =
    [
        ("POLICY_RATE",
        [
            ("USD", 3.00m, 3.00m, 3.25m), ("EUR", 4.00m, 3.75m, 3.75m),
            ("GBP", 4.25m, 4.25m, 4.25m), ("JPY", 1.25m, 1.00m, 1.00m),
            ("AUD", 3.85m, 3.85m, 3.85m), ("CHF", 0.50m, 0.50m, 0.75m),
            ("CAD", 2.50m, 2.50m, 2.75m), ("NZD", 3.00m, 3.00m, 3.25m)
        ]),
        ("CPI_YOY",
        [
            ("USD", 2.7m, 2.8m, 3.0m), ("EUR", 2.4m, 2.1m, 2.2m),
            ("GBP", 3.6m, 3.3m, 3.4m), ("JPY", 2.9m, 2.9m, 2.8m),
            ("AUD", 2.5m, 2.7m, 2.8m), ("CHF", 0.4m, 0.6m, 0.7m),
            ("CAD", 1.9m, 1.9m, 2.0m), ("NZD", null, 2.2m, 2.3m)
        ]),
        // Nothing has printed this quarter — this is the coverage story.
        ("GDP_QOQ",
        [
            ("USD", null, 2.1m, 2.4m), ("EUR", null, 0.3m, 0.4m),
            ("GBP", null, 0.2m, 0.3m), ("JPY", null, 0.2m, 0.1m),
            ("AUD", null, 0.5m, 0.6m), ("CHF", null, 0.3m, 0.3m),
            ("CAD", null, 0.4m, 0.5m), ("NZD", null, 0.2m, 0.2m)
        ]),
        ("PMI_MFG",
        [
            ("USD", 46.8m, 49.0m, 48.6m), ("EUR", 51.8m, 50.9m, 50.4m),
            ("GBP", 50.2m, 50.4m, 50.1m), ("JPY", 49.9m, 49.1m, 48.8m),
            ("AUD", 50.6m, 50.5m, 50.2m), ("CHF", 48.9m, 49.2m, 48.5m),
            ("CAD", 47.5m, 48.8m, 48.3m), ("NZD", null, 49.5m, 49.2m)
        ]),
        ("NFP", [("USD", 185m, 145m, 158m)]),
        ("EMPLOY_CHANGE",
        [
            ("EUR", 21m, 20m, 24m), ("AUD", 34m, 18m, 22m), ("CAD", -8m, 12m, 15m)
        ]),
        // USD is here as well as in NFP: profile v2 scores every currency on the
        // unemployment rate, so a fixture without a USD rate would leave the
        // labour factor unavailable on every pair and untested.
        ("UNEMPLOYMENT",
        [
            ("USD", 4.2m, 4.1m, 4.1m),
            ("GBP", 4.6m, 4.4m, 4.4m), ("JPY", 2.3m, 2.5m, 2.5m),
            ("CHF", 2.8m, 2.8m, 2.7m), ("NZD", 4.9m, 4.9m, 4.8m)
        ]),
        ("RETAIL_MOM",
        [
            ("USD", 0.6m, 0.4m, 0.3m), ("EUR", -0.1m, 0.2m, 0.1m),
            ("GBP", 0.8m, 0.3m, 0.2m), ("JPY", 0.2m, 0.2m, 0.3m),
            ("AUD", 0.5m, 0.3m, 0.2m), ("CHF", 0.1m, 0.1m, 0.2m),
            ("CAD", -0.4m, 0.1m, 0.2m), ("NZD", null, 0.2m, 0.1m)
        ]),
        // No forecast: positioning is scored against the previous print, which
        // the normalizer's fallback handles and the explanation discloses.
        ("COT_NET",
        [
            ("USD", -12.4m, null, -6.1m), ("EUR", 48.2m, null, 39.7m),
            ("GBP", 31.5m, null, 24.9m), ("JPY", 62.8m, null, 41.3m),
            ("AUD", 3.1m, null, 2.4m), ("CHF", -18.6m, null, -14.2m),
            ("CAD", -22.4m, null, -19.8m), ("NZD", -1.2m, null, 0.6m)
        ])
    ];

    /// <summary>
    /// Enough readings for the v2.0.0 level component to engage — below
    /// LevelTrendNormalizer.MinimumHistory the fixture would only ever exercise
    /// the direction half of the normalizer.
    /// </summary>
    private const int HistoryPeriods = 24;

    /// <summary>Deterministic: golden-file tests assert exact scores.</summary>
    private static double Noise(string key, int index)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in key) h = (h ^ c) * 16777619u;
            h = (h ^ (uint)index) * 16777619u;
            return (h % 1000u) / 1000.0;
        }
    }

    private static decimal Amplitude(IndicatorUnit unit) => unit switch
    {
        IndicatorUnit.Thousands => 40m,
        IndicatorUnit.Index => 1.5m,
        _ => 0.30m
    };

    public static List<IndicatorRelease> Releases(List<Indicator> indicators, DateTimeOffset asOf)
    {
        var byCode = indicators.ToDictionary(i => i.Code, StringComparer.Ordinal);
        var period = DateOnly.FromDateTime(asOf.UtcDateTime).AddMonths(-1);
        var releases = new List<IndicatorRelease>();
        var offset = 2;

        foreach (var (code, rows) in ReleaseTable)
        {
            var indicator = byCode[code];
            foreach (var (currency, actual, forecast, previous) in rows)
            {
                // Scatter around the current level rather than a cumulative walk:
                // a drifting walk wanders into implausible territory over 24
                // periods and makes the level percentile meaningless.
                //
                // Critically, the two most recent readings are the fixture's own
                // stated values. History supplies the LEVEL component; the stated
                // actual/previous still supply DIRECTION, so the documented
                // scenario ("the Fed cut 25bp") survives intact.
                var walk = new decimal[HistoryPeriods];
                if (actual is { } current)
                {
                    walk[^1] = current;
                    walk[^2] = previous ?? current;

                    var amp = Amplitude(indicator.Unit);
                    for (var i = 0; i < HistoryPeriods - 2; i++)
                    {
                        var offsetFromLevel = (decimal)(Noise($"{code}:{currency}", i) - 0.5) * amp * 4m;
                        walk[i] = Math.Round(current + offsetFromLevel, 4);
                    }

                    for (var i = 0; i < HistoryPeriods - 1; i++)
                    {
                        releases.Add(new IndicatorRelease
                        {
                            Id = Id($"release:{code}:{currency}:h{i}"),
                            IndicatorId = indicator.Id,
                            CurrencyCode = currency,
                            Period = period.AddMonths(-(HistoryPeriods - 1 - i)),
                            Actual = walk[i],
                            Forecast = i > 0 ? walk[i - 1] : null,
                            Previous = i > 0 ? walk[i - 1] : null,
                            Revision = 0,
                            Source = DataSource.Manual,
                            SourceRef = "seed-history",
                            ReleasedAt = asOf.AddMonths(-(HistoryPeriods - 1 - i)).AddHours(-offset),
                            ImportedAt = asOf
                        });
                    }
                }

                releases.Add(new IndicatorRelease
                {
                    Id = Id($"release:{code}:{currency}"),
                    IndicatorId = indicator.Id,
                    CurrencyCode = currency,
                    Period = period,
                    Actual = actual,
                    Forecast = forecast,
                    // The fixture's own stated previous, untouched by the
                    // generated history.
                    Previous = previous,
                    Revision = 0,
                    Source = DataSource.Manual,
                    SourceRef = "seed",
                    ReleasedAt = asOf.AddHours(-offset),
                    ImportedAt = asOf
                });

                // Next period, scheduled but not yet printed. A real calendar is
                // mostly forward-looking, and the engine must ignore these — they
                // are excluded by ReleasedAt > asOf, and by the Actual-first
                // ordering in ScoringDataLoader once their time passes.
                releases.Add(new IndicatorRelease
                {
                    Id = Id($"release:{code}:{currency}:next"),
                    IndicatorId = indicator.Id,
                    CurrencyCode = currency,
                    Period = period.AddMonths(1),
                    Actual = null,
                    Forecast = actual ?? forecast,
                    Previous = actual ?? previous,
                    Revision = 0,
                    Source = DataSource.Manual,
                    SourceRef = "seed",
                    ReleasedAt = asOf.AddHours(offset * 2),
                    ImportedAt = asOf
                });

                offset += 1;
            }
        }

        return releases;
    }

    /// <summary>
    /// A linear ramp with the newest value planted at a chosen percentile, so the
    /// PercentileNormalizer lands on a known bucket. Deliberately not random:
    /// the golden-file tests assert exact scores.
    /// </summary>
    public static List<SeriesObservation> Observations(List<MarketSeries> series, DateTimeOffset asOf)
    {
        var observations = new List<SeriesObservation>();

        foreach (var s in series)
        {
            var (low, high, latest) = s.Code switch
            {
                "DXY" => (94.00m, 110.00m, 96.42m),
                _ => (0.80m, 2.60m, 1.12m)
            };

            const int history = 251;
            var step = (high - low) / (history - 1);

            for (var i = 0; i < history; i++)
            {
                observations.Add(new SeriesObservation
                {
                    Id = Id($"obs:{s.Code}:{i}"),
                    SeriesId = s.Id,
                    ObservedAt = asOf.AddDays(-(history - i)),
                    Value = low + step * i,
                    Source = DataSource.Manual
                });
            }

            observations.Add(new SeriesObservation
            {
                Id = Id($"obs:{s.Code}:latest"),
                SeriesId = s.Id,
                ObservedAt = asOf.AddHours(-1),
                Value = latest,
                Source = DataSource.Manual
            });
        }

        return observations;
    }
}
