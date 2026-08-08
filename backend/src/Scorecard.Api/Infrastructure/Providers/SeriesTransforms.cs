using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Providers;

public sealed record DerivedReading(DateOnly Period, decimal Value);

/// <summary>
/// Turns a provider's raw series into the reading a trader would recognise.
///
/// FRED publishes CPI as an index and payrolls as a level; nobody quotes those.
/// The derivation runs once at ingestion and the result is stored, so a score is
/// always explainable from a persisted number rather than a recomputation that
/// might drift.
/// </summary>
public static class SeriesTransforms
{
    public static IReadOnlyList<DerivedReading> Apply(
        SeriesTransform transform,
        IReadOnlyList<ProviderObservation> observations)
    {
        if (observations.Count == 0) return [];

        var ordered = observations.OrderBy(o => o.Date).ToList();

        return transform switch
        {
            SeriesTransform.Level =>
                ordered.Select(o => new DerivedReading(o.Date, o.Value)).ToList(),

            SeriesTransform.LevelMonthly =>
                MonthlyLast(ordered),

            SeriesTransform.PeriodChange =>
                Pairwise(ordered, (current, prior) => current - prior),

            SeriesTransform.PeriodOverPeriodPercent =>
                Pairwise(ordered, (current, prior) =>
                    prior == 0m ? 0m : Math.Round((current / prior - 1m) * 100m, 4)),

            SeriesTransform.YearOverYearPercent =>
                YearOverYear(ordered),

            // Blends are combined before this point, so by here they are levels.
            SeriesTransform.BlendAverage =>
                ordered.Select(o => new DerivedReading(o.Date, o.Value)).ToList(),

            _ => ordered.Select(o => new DerivedReading(o.Date, o.Value)).ToList()
        };
    }

    /// <summary>
    /// Collapses a daily series to one reading per month, stamped to the first of
    /// the month so it lines up with genuinely monthly indicators.
    /// </summary>
    private static List<DerivedReading> MonthlyLast(List<ProviderObservation> ordered) =>
        ordered
            .GroupBy(o => new DateOnly(o.Date.Year, o.Date.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new DerivedReading(g.Key, g.OrderBy(o => o.Date).Last().Value))
            .ToList();

    private static List<DerivedReading> Pairwise(
        List<ProviderObservation> ordered,
        Func<decimal, decimal, decimal> combine)
    {
        var result = new List<DerivedReading>(Math.Max(0, ordered.Count - 1));
        for (var i = 1; i < ordered.Count; i++)
            result.Add(new DerivedReading(ordered[i].Date, combine(ordered[i].Value, ordered[i - 1].Value)));
        return result;
    }

    /// <summary>
    /// Matched by calendar date rather than by index, so a gap in the series
    /// cannot silently shift the comparison to the wrong month.
    /// </summary>
    private static List<DerivedReading> YearOverYear(List<ProviderObservation> ordered)
    {
        var byDate = ordered.ToDictionary(o => o.Date, o => o.Value);
        var result = new List<DerivedReading>();

        foreach (var o in ordered)
        {
            var yearAgo = o.Date.AddYears(-1);
            if (!byDate.TryGetValue(yearAgo, out var prior) || prior == 0m) continue;
            result.Add(new DerivedReading(o.Date, Math.Round((o.Value / prior - 1m) * 100m, 4)));
        }

        return result;
    }

    /// <summary>Averages several series on the dates they share.</summary>
    public static IReadOnlyList<ProviderObservation> Blend(
        IReadOnlyList<IReadOnlyList<ProviderObservation>> series)
    {
        if (series.Count == 0) return [];
        if (series.Count == 1) return series[0];

        var maps = series.Select(s => s.ToDictionary(o => o.Date, o => o.Value)).ToList();

        // Intersection only: averaging a date that one survey has not published
        // yet would make the blend jump for a reason unrelated to the economy.
        var shared = maps.Skip(1)
            .Aggregate(new HashSet<DateOnly>(maps[0].Keys), (acc, m) => { acc.IntersectWith(m.Keys); return acc; });

        return shared
            .OrderBy(d => d)
            .Select(d => new ProviderObservation(d, Math.Round(maps.Average(m => m[d]), 4)))
            .ToList();
    }
}
