using System.Globalization;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Scoring;

/// <summary>
/// Raw-value display text, generated once at scoring time alongside the number
/// it describes. Stored on the factor row so a later data revision can never
/// change what a historical score appears to have been based on (rule R4).
/// </summary>
public static class Labels
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Value(decimal? value, IndicatorUnit unit) => value switch
    {
        null => "—",
        _ => unit switch
        {
            IndicatorUnit.Percent or IndicatorUnit.PercentagePoints =>
                value.Value.ToString("0.0", Inv) + "%",
            IndicatorUnit.Thousands =>
                (value.Value > 0 ? "+" : "") + value.Value.ToString("0", Inv) + "K",
            _ => value.Value.ToString("0.0", Inv)
        }
    };

    /// <summary>
    /// v2.0.0 labels read as level-and-direction, not surprise-vs-consensus.
    /// When history was too short for a percentile, the label says so rather
    /// than implying a level was measured.
    /// </summary>
    public static (string Mn, string En) Release(ReleaseSnapshot snapshot, bool levelUsed)
    {
        var unit = snapshot.Indicator.Unit;
        var actual = Value(snapshot.Release.Actual, unit);
        var previous = Value(snapshot.Release.Previous, unit);

        if (!levelUsed)
            return ($"{actual} (өмнөх {previous})", $"{actual} (prev. {previous})");

        var current = snapshot.Release.Actual ?? 0m;
        var below = snapshot.History.Count(v => v < current);
        var percentile = snapshot.History.Count == 0
            ? 0
            : (int)Math.Round((decimal)below / snapshot.History.Count * 100m);

        // The sample size is stated because the percentile alone overstates its
        // own precision: "0th percentile" from 16 readings and from 60 are very
        // different claims, and the reader cannot tell them apart otherwise.
        var n = snapshot.History.Count;

        return (
            $"{actual} · {n} уншилтын {percentile}-р хувиар (өмнөх {previous})",
            $"{actual} · {Ordinal(percentile)} percentile of {n} readings (prev. {previous})");
    }

    /// <summary>
    /// English ordinal suffix. Mongolian needs none — "-р" is invariant — which
    /// is why this only wraps the English half.
    /// </summary>
    internal static string Ordinal(int n)
    {
        // 11th, 12th, 13th are the exceptions the last-digit rule gets wrong.
        var suffix = (n % 100) is >= 11 and <= 13
            ? "th"
            : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

        return $"{n}{suffix}";
    }

    /// <summary>
    /// Inflation read against the mandate. States the gap in points rather than a
    /// percentile, because "1.5pp above target" is a sentence a trader can act on
    /// and "79th percentile of history" is not.
    /// </summary>
    public static (string Mn, string En) InflationTarget(
        decimal actual, decimal? previous, CurrencyPolicy policy)
    {
        var value = actual.ToString("0.0", Inv) + "%";
        var target = policy.InflationTarget.ToString("0.0", Inv) + "%";
        var gap = actual - policy.InflationTarget;

        var side = Math.Abs(gap) <= policy.ToleranceBand
            ? ("зорилтдоо", "at target")
            : gap > 0
                ? ($"зорилтоос {Math.Abs(gap).ToString("0.0", Inv)}пп дээгүүр",
                   $"{Math.Abs(gap).ToString("0.0", Inv)}pp above target")
                : ($"зорилтоос {Math.Abs(gap).ToString("0.0", Inv)}пп доогуур",
                   $"{Math.Abs(gap).ToString("0.0", Inv)}pp below target");

        if (previous is not { } prior)
            return ($"{value} · {side.Item1} ({target})", $"{value} · {side.Item2} ({target})");

        // Converging or diverging is the half of the reading that says what the
        // bank does next, so it is always stated.
        var priorGap = prior - policy.InflationTarget;
        var move = Math.Abs(gap) - Math.Abs(priorGap);

        var trend = move switch
        {
            0m => ("өөрчлөлтгүй", "unchanged"),
            < 0m => ("зорилт руу ойртож байна", "converging"),
            _ => ("зорилтоос холдож байна", "diverging")
        };

        return (
            $"{value} · {side.Item1} ({target}), {trend.Item1}",
            $"{value} · {side.Item2} ({target}), {trend.Item2}");
    }

    public static (string Mn, string En) PolicyRate(ReleaseSnapshot snapshot)
    {
        var actual = snapshot.Release.Actual ?? 0m;
        var previous = snapshot.Release.Previous;
        var level = actual.ToString("0.00", Inv) + "%";

        if (previous is not { } prev || prev == actual)
            return ($"{level} · өөрчлөлтгүй", $"{level} · unchanged");

        var bp = Math.Abs((actual - prev) * 100m).ToString("0", Inv);
        return actual > prev
            ? ($"{level} · {bp}bp өсгөв", $"{level} · hiked {bp}bp")
            : ($"{level} · {bp}bp бууруулав", $"{level} · cut {bp}bp");
    }

    public static (string Mn, string En) Series(decimal value, decimal percentile, MarketSeries series)
    {
        var v = value.ToString("0.00", Inv);
        var p = (int)Math.Round(percentile * 100m, 0);

        // The qualifier travels with the number, not with the column heading: a
        // reader comparing 119.70 against the DXY on their own screen needs to
        // know at that moment that this is the Fed's broad index, not the ICE one.
        var mn = string.IsNullOrEmpty(series.ScaleNote.Mn) ? v : $"{v} ({series.ScaleNote.Mn})";
        var en = string.IsNullOrEmpty(series.ScaleNote.En) ? v : $"{v} ({series.ScaleNote.En})";

        return ($"{mn} · 1 жилийн {p}-р хувиар", $"{en} · {Ordinal(p)} percentile of 1Y");
    }
}

/// <summary>
/// Bilingual explanation templates, versioned with the engine. Generated at
/// calculation time and stored — an explanation is a record of a calculation,
/// not a live re-render.
/// </summary>
public static class Explanations
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, (string Mn, string En)> CountryNames = new()
    {
        ["USD"] = ("АНУ", "US"),
        ["EUR"] = ("Евро бүс", "Eurozone"),
        ["GBP"] = ("Их Британи", "UK"),
        ["JPY"] = ("Япон", "Japan"),
        ["AUD"] = ("Австрали", "Australia"),
        ["CHF"] = ("Швейцар", "Switzerland"),
        ["CAD"] = ("Канад", "Canada"),
        ["NZD"] = ("Шинэ Зеланд", "New Zealand")
    };

    public const string UnavailableMn =
        "Өгөгдөл байхгүй эсвэл хэт хуучирсан тул энэ хүчин зүйл тооцоололд ороогүй. Хамралтын хувь буурсан.";

    public const string UnavailableEn =
        "No usable data for this factor, so it was excluded from the calculation. Coverage is reduced accordingly.";

    public static (string Mn, string En) Compose(
        Factor factor,
        Asset asset,
        FactorEvaluation evaluation,
        decimal weight,
        short polarity,
        decimal contribution)
    {
        var n = evaluation.Normalized;
        var tailMn = $"Хэвийн оноо {Signed(n)} · жин {weight.ToString("0.##", Inv)} · нөлөөлөл {Signed(contribution)}.";
        var tailEn = $"Normalized {Signed(n)} · weight {weight.ToString("0.##", Inv)} · contribution {Signed(contribution)}.";

        if (evaluation.Readings.Count == 2)
        {
            var b = evaluation.Readings.First(r => r.Direction == 1);
            var q = evaluation.Readings.First(r => r.Direction == -1);
            var (bMn, bEn) = Country(b.Currency);
            var (qMn, qEn) = Country(q.Currency);

            return (
                $"{bMn}: {b.Reading.LabelMn}. {qMn}: {q.Reading.LabelMn}. Зөрүү нь {asset.Symbol}-д {DirMn(n)}. {tailMn}",
                $"{bEn}: {b.Reading.LabelEn}. {qEn}: {q.Reading.LabelEn}. The differential is {DirEn(n)} for {asset.Symbol}. {tailEn}");
        }

        var only = evaluation.Readings[0];
        var (cMn, cEn) = Country(only.Currency);
        var inverted = only.Direction == -1;

        var relationMn = inverted
            ? $"{only.Currency} нь {asset.Symbol}-ийн ханшийн валют тул нөлөө нь урвуу."
            : $"{asset.Symbol} нь {only.Currency}-тай шууд хамааралтай.";
        var relationEn = inverted
            ? $"{only.Currency} is the quote currency for {asset.Symbol}, so the effect inverts."
            : $"{asset.Symbol} tracks {only.Currency} directly.";

        var polarityMn = polarity == -1 ? $" {asset.Symbol} нь энэ хүчин зүйлд урвуу хариу үзүүлдэг." : "";
        var polarityEn = polarity == -1 ? $" {asset.Symbol} responds inversely to this factor." : "";

        return (
            $"{cMn} — {factor.Name.Mn}: {only.Reading.LabelMn}. {relationMn}{polarityMn} {tailMn}",
            $"{cEn} — {factor.Name.En}: {only.Reading.LabelEn}. {relationEn}{polarityEn} {tailEn}");
    }

    private static (string Mn, string En) Country(string currency) =>
        CountryNames.TryGetValue(currency, out var v) ? v : (currency, currency);

    private static string Signed(short v) => v > 0 ? $"+{v}" : v.ToString(Inv);

    private static string Signed(decimal v) =>
        (v > 0 ? "+" : "") + v.ToString("0.0", Inv);

    private static string DirMn(decimal n) => n > 0 ? "эерэг" : n < 0 ? "сөрөг" : "төвийг сахисан";

    private static string DirEn(decimal n) => n > 0 ? "positive" : n < 0 ? "negative" : "neutral";
}
