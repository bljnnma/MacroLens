using Scorecard.Api.Domain;
using Scorecard.Api.Scoring.Contributors;

namespace Scorecard.Api.Infrastructure.Database.Seed;

public sealed record SeedBundle(
    List<Factor> Factors,
    List<Indicator> Indicators,
    List<Asset> Assets,
    List<MarketSeries> Series,
    List<ScoringProfile> Profiles,
    List<IndicatorRelease> Releases,
    List<SeriesObservation> Observations);

/// <summary>
/// Reasons a currency's CPI is still on fixture data, so the gap is recorded in
/// code rather than only in a commit message.
/// </summary>
public static class C3bGaps
{
    public const string JpyCpi =
        "OECD's Japanese CPI series ends 2021-06; e-Stat requires a registered API key.";
}

/// <summary>
/// Reference data plus one coherent macro fixture: a soft landing. The Fed is
/// cutting into resilient labour, US inflation is undershooting, the dollar and
/// real yields sit near one-year lows, and the ECB is still tightening.
///
/// Values are chosen to reproduce `c` under the bands in scoring-spec.md §4 —
/// e.g. US CPI 2.7 vs 2.8 gives d = -0.10, which meets band_minor but not
/// band_major, hence -1. The whole set is exercised by the golden-file tests.
/// </summary>
public static partial class SeedData
{
    public static readonly string[] Currencies = ["USD", "EUR", "GBP", "JPY", "AUD", "CHF", "CAD", "NZD"];

    private static Guid Id(string key) => new(System.Security.Cryptography.MD5.HashData(
        System.Text.Encoding.UTF8.GetBytes(key)));

    private static LocalizedText L(string mn, string en) => new(mn, en);

    public static SeedBundle Build(DateTimeOffset asOf)
    {
        var factors = Factors();
        var indicators = Indicators();
        var assets = Assets();
        var series = Series();
        var profiles = Profiles(asOf);
        var releases = Releases(indicators, asOf);
        var observations = Observations(series, asOf);

        return new SeedBundle(factors, indicators, assets, series, profiles, releases, observations);
    }

    /// <summary>
    /// Published central bank mandates. Tolerance is half the official band where
    /// one exists, otherwise 0.5pp.
    ///
    /// Sourced from each bank's own mandate, not from a data provider — these are
    /// legal or statutory objectives that change once a decade, and treating them
    /// as a feed would add an integration for a constant.
    /// </summary>
    public static List<CurrencyPolicy> CurrencyPolicies() =>
    [
        // Point targets, no published band.
        P("USD", 2.0m, 0.5m, "Холбооны нөөцийн систем", "Federal Reserve"),
        P("EUR", 2.0m, 0.5m, "Европын Төв Банк", "European Central Bank"),
        P("JPY", 2.0m, 0.5m, "Японы Банк", "Bank of Japan"),

        // Point target with an official ±1pp threshold: outside it the Governor
        // must write to the Chancellor, which is as explicit as a band gets.
        P("GBP", 2.0m, 1.0m, "Английн Банк", "Bank of England"),

        // Published bands — target is the midpoint, tolerance is half the width.
        P("AUD", 2.5m, 0.5m, "Австралийн Нөөцийн Банк", "Reserve Bank of Australia"),
        P("NZD", 2.0m, 1.0m, "Шинэ Зеландын Нөөцийн Банк", "Reserve Bank of New Zealand"),
        P("CAD", 2.0m, 1.0m, "Канадын Банк", "Bank of Canada"),

        // "Below 2%" — a 0-2% range, so the midpoint is 1.0.
        P("CHF", 1.0m, 1.0m, "Швейцарийн Үндэсний Банк", "Swiss National Bank")
    ];

    private static CurrencyPolicy P(string code, decimal target, decimal tolerance, string mn, string en) =>
        new()
        {
            CurrencyCode = code,
            InflationTarget = target,
            ToleranceBand = tolerance,
            Authority = L(mn, en)
        };

    public static List<Factor> Factors() =>
    [
        F(FactorCodes.Rate, 1, FactorCategory.Policy, FactorScope.CurrencyScoped,
            L("Бодлогын хүү", "Policy rate"), L("Хүү", "Rate"),
            L("Төв банкны бодлогын хүүгийн түвшин ба чиглэл.", "Central bank policy rate level and direction.")),
        F(FactorCodes.Cpi, 2, FactorCategory.Inflation, FactorScope.CurrencyScoped,
            L("Инфляци", "Inflation"), L("CPI", "CPI"),
            L("Хэрэглээний үнийн индекс.", "Consumer price index.")),
        F(FactorCodes.Gdp, 3, FactorCategory.Growth, FactorScope.CurrencyScoped,
            L("ДНБ-ий өсөлт", "GDP growth"), L("GDP", "GDP"),
            L("Улирлын нийт бүтээгдэхүүний өсөлт.", "Quarter-on-quarter output growth.")),
        F(FactorCodes.Pmi, 4, FactorCategory.Growth, FactorScope.CurrencyScoped,
            L("Худалдан авагчийн индекс", "PMI"), L("PMI", "PMI"),
            L("50-аас дээш бол тэлэлт.", "Above 50 signals expansion.")),
        // LABOUR, not NFP: the factor is scored from the harmonised unemployment
        // rate for every currency, and a heatmap column reading "NFP" above a
        // 6.3% figure would look like a payrolls number.
        F(FactorCodes.Labour, 5, FactorCategory.Labour, FactorScope.CurrencyScoped,
            L("Хөдөлмөрийн зах зээл", "Labour market"), L("LABOUR", "LABOUR"),
            L("Ажилгүйдлийн түвшин. Өсөх нь валютыг сулруулна.",
              "Unemployment rate. A rising rate weakens the currency.")),

        // Retired in profile v2 but still present: historical factor rows are
        // stamped NFP and the catalogue must keep explaining what they were.
        F(FactorCodes.Nfp, 50, FactorCategory.Labour, FactorScope.CurrencyScoped,
            L("Хөдөө аж ахуйн бус ажлын байр (хуучирсан)", "Non-Farm Payrolls (retired)"),
            L("NFP", "NFP"),
            L("Профайл v2-т LABOUR-аар солигдсон. Хүлээлттэй харьцуулах өгөгдөл үнэгүй байдаггүй тул оноололтод ашиглагдахгүй.",
              "Replaced by LABOUR in profile v2. Not scored: its signal lives in the surprise against consensus, which no free provider publishes.")),
        F(FactorCodes.Retail, 6, FactorCategory.Growth, FactorScope.CurrencyScoped,
            L("Жижиглэн худалдаа", "Retail sales"), L("Retail", "Retail"),
            L("Хэрэглэгчийн эрэлтийн шууд хэмжүүр.", "The most direct read on consumer demand.")),
        F(FactorCodes.Dxy, 7, FactorCategory.Sentiment, FactorScope.UsdScoped,
            L("Долларын хүч", "Dollar strength"), L("DXY", "DXY"),
            L("Долларын индексийн 1 жилийн хуваарилалт дахь байрлал. Fed-ийн өргөн индексээр хэмжинэ — арилжааны платформ дээрх ICE DXY-тэй тоо нь таарахгүй.",
              "Where the dollar sits in its one-year distribution, measured on the Fed's broad index — the number will not match the ICE DXY on a trading platform.")),
        F(FactorCodes.Yield, 8, FactorCategory.Policy, FactorScope.UsdScoped,
            L("Бодит өгөөж", "Real yield"), L("Yield", "Yield"),
            L("АНУ-ын 10 жилийн бодит өгөөж.", "US 10-year real yield.")),
        F(FactorCodes.Cot, 9, FactorCategory.Positioning, FactorScope.CurrencyScoped,
            L("Позиц байрлал (COT)", "Positioning (COT)"), L("COT", "COT"),
            L("CFTC-ийн спекулятив цэвэр позиц. Эдийн засгийн мэдээлэл биш.",
              "Net speculative positioning from the CFTC report. Not an economic release."))
    ];

    private static Factor F(string code, int order, FactorCategory cat, FactorScope scope,
        LocalizedText name, LocalizedText shortName, LocalizedText desc) =>
        new()
        {
            Id = Id($"factor:{code}"),
            Code = code,
            DisplayOrder = order,
            Category = cat,
            Scope = scope,
            Name = name,
            ShortName = shortName,
            Description = desc
        };

    public static List<Indicator> Indicators()
    {
        var indicators = BaseIndicators();

        foreach (var indicator in indicators)
        {
            if (!UsdProviderMap.TryGetValue(indicator.Code, out var mapping)) continue;
            indicator.ProviderSeriesId = mapping.Series;
            indicator.Transform = mapping.Transform;
            indicator.IsProxy = mapping.IsProxy;
            indicator.MaxAgeDays = mapping.MaxAgeDays;
        }

        return indicators;
    }

    private static List<Indicator> BaseIndicators() =>
    [
        I("POLICY_RATE", FactorCodes.Rate, FactorCategory.Policy, 1, IndicatorUnit.Percent, 0.10m, 0.25m, 400, Impact.High,
            L("Бодлогын хүү", "Policy rate"),
            L("Төв банкны суурь хүү.", "The central bank's benchmark rate.")),
        // 130 days, not 60: after C3b this indicator serves seven currencies and
        // New Zealand publishes CPI quarterly. A monthly-sized window would make
        // NZD's CPI permanently unavailable. The cost is that a genuinely late
        // monthly print is tolerated longer than it should be — MaxAgeDays lives
        // on the indicator, so it cannot yet differ per currency.
        I("CPI_YOY", FactorCodes.Cpi, FactorCategory.Inflation, 1, IndicatorUnit.PercentagePoints, 0.10m, 0.30m, 130, Impact.High,
            L("Хэрэглээний үнийн индекс (жилээр)", "Consumer Price Index (YoY)"),
            L("Барааны сагсны жилийн үнийн өөрчлөлт.", "Year-on-year change in a basket of goods.")),
        I("GDP_QOQ", FactorCodes.Gdp, FactorCategory.Growth, 1, IndicatorUnit.PercentagePoints, 0.20m, 0.60m, 130, Impact.High,
            L("ДНБ-ий улирлын өсөлт", "GDP (QoQ)"),
            L("Өмнөх улиралтай харьцуулсан өөрчлөлт.", "Change against the previous quarter.")),
        I("PMI_MFG", FactorCodes.Pmi, FactorCategory.Growth, 1, IndicatorUnit.Index, 0.50m, 1.50m, 60, Impact.High,
            L("Үйлдвэрлэлийн идэвхийн орлуулагч (PMI Proxy)", "Manufacturing Activity Proxy (PMI Proxy)"),
            L("ISM-ийн PMI нь лицензтэй тул нээлттэй эх сурвалжид байдаггүй. Үүний оронд Нью-Йорк болон Даллас дахь Холбооны Нөөцийн бүсийн үйлдвэрлэлийн судалгааны дундажийг ашиглана. Энэ нь ойролцоо утга бөгөөд жинхэнэ PMI биш.",
              "ISM's PMI is licensed and not redistributable, so this blends the New York and Dallas Fed regional manufacturing surveys instead. It is a proxy for the same signal, not the PMI itself.")),
        I("NFP", FactorCodes.Nfp, FactorCategory.Labour, 1, IndicatorUnit.Thousands, 25m, 75m, 60, Impact.High,
            L("Хөдөө аж ахуйн бус ажлын байр", "Non-Farm Payrolls"),
            L("АНУ-ын сарын ажлын байрны өөрчлөлт.", "Monthly US payroll change.")),
        I("EMPLOY_CHANGE", FactorCodes.Labour, FactorCategory.Labour, 1, IndicatorUnit.Thousands, 10m, 30m, 60, Impact.Medium,
            L("Хөдөлмөр эрхлэлтийн өөрчлөлт", "Employment Change"),
            L("АНУ-аас бусад валютын ажлын байрны өөрчлөлт.", "Employment change outside the US.")),
        // Higher unemployment weakens a currency, so this one inverts.
        //
        // 150 days, not 60: after the labour integration this indicator serves
        // seven currencies and their publication lags differ sharply — the euro
        // area lands two months after the period, the UK four, and Switzerland's
        // harmonised rate three to four. A 60-day window would make the slower
        // reporters permanently unavailable. The scheduler's overdue flag
        // (45 days without a change, see architecture.md §6.5) is the watchdog
        // for a genuinely dead feed; MaxAgeDays only decides usability.
        I("UNEMPLOYMENT", FactorCodes.Labour, FactorCategory.Labour, -1, IndicatorUnit.PercentagePoints, 0.10m, 0.30m, 150, Impact.Medium,
            L("Ажилгүйдлийн түвшин", "Unemployment Rate"),
            L("Ажилгүйчүүдийн эзлэх хувь.", "Share of the labour force out of work.")),
        I("RETAIL_MOM", FactorCodes.Retail, FactorCategory.Growth, 1, IndicatorUnit.PercentagePoints, 0.20m, 0.50m, 60, Impact.Medium,
            L("Жижиглэн худалдаа (сараар)", "Retail Sales (MoM)"),
            L("Борлуулалтын сарын өөрчлөлт.", "Month-on-month change in retail sales.")),
        I("COT_NET", FactorCodes.Cot, FactorCategory.Positioning, 1, IndicatorUnit.Thousands, 2.0m, 15.0m, 30, Impact.Low,
            L("COT цэвэр позиц", "COT Net Positioning"),
            L("Долоо хоног тутмын спекулятив цэвэр позиц.", "Weekly net speculative positioning.")),
        I("DXY_INDEX", FactorCodes.Dxy, FactorCategory.Sentiment, 1, IndicatorUnit.Index, 0m, 0m, 5, Impact.Medium,
            L("АНУ долларын өргөн индекс (Fed)", "US Dollar Index (Fed broad)"),
            L("Холбооны нөөцийн банкны худалдаагаар жинлэсэн индекс, 2006 он = 100. ICE DXY биш.",
              "The Federal Reserve's broad trade-weighted index, 2006 = 100. Not the ICE DXY.")),
        I("US10Y_REAL", FactorCodes.Yield, FactorCategory.Policy, 1, IndicatorUnit.Percent, 0m, 0m, 5, Impact.High,
            L("АНУ-ын 10 жилийн бодит өгөөж", "US 10Y Real Yield"),
            L("Нэрлэсэн өгөөжөөс инфляцийн хүлээлтийг хассан утга.", "Nominal yield minus breakeven inflation."))
    ];

    private static Indicator I(string code, string factorCode, FactorCategory cat, short direction,
        IndicatorUnit unit, decimal minor, decimal major, int maxAge, Impact impact,
        LocalizedText name, LocalizedText desc) =>
        new()
        {
            Id = Id($"indicator:{code}"),
            Code = code,
            FactorCode = factorCode,
            Category = cat,
            CurrencyDirection = direction,
            Unit = unit,
            BandMinor = minor,
            BandMajor = major,
            MaxAgeDays = maxAge,
            Impact = impact,
            Name = name,
            Description = desc,
            WhyItMatters = desc,
            HowItAffects = desc
        };

    /// <summary>
    /// FRED backing for the USD indicators — the C3a scope. Everything here is
    /// free and carries no redistribution restriction, unlike the licensed
    /// consensus and PMI products.
    ///
    /// Keyed by indicator code; the currency is always USD because FRED's
    /// international coverage is stale or discontinued (see docs).
    /// </summary>
    public static readonly Dictionary<string, (string Series, SeriesTransform Transform, bool IsProxy, int MaxAgeDays)> UsdProviderMap =
        new(StringComparer.Ordinal)
        {
            // Fed funds target upper bound. Published daily but resampled to
            // month-end: scored daily, a rate that moves twice a year would show
            // no direction on 99% of days.
            ["POLICY_RATE"] = ("DFEDTARU", SeriesTransform.LevelMonthly, false, 400),

            // Index -> year-over-year percent, which is the figure traders quote.
            ["CPI_YOY"] = ("CPIAUCSL", SeriesTransform.YearOverYearPercent, false, 75),

            // NFP/PAYEMS is deliberately absent. Payrolls carry their signal in
            // the surprise against consensus, which no free provider publishes,
            // and scoring USD on payrolls while every other currency is scored on
            // the unemployment rate would compare two different statistics across
            // a pair. USD now uses the same harmonised rate as the rest; NFP
            // remains in the catalogue and on the calendar.

            ["RETAIL_MOM"] = ("RSAFS", SeriesTransform.PeriodOverPeriodPercent, false, 75),

            // Quarterly: 130 days covers the lag between quarter end and release.
            ["GDP_QOQ"] = ("GDPC1", SeriesTransform.PeriodOverPeriodPercent, false, 200),

            // ISM is licensed and absent from FRED. Empire State and Dallas Fed
            // are free, current, and directionally the same signal — blended and
            // labelled a proxy rather than passed off as PMI.
            ["PMI_MFG"] = ("GACDISA066MSFRBNY,BACTSAMFRBDAL", SeriesTransform.BlendAverage, true, 75)
        };

    /// <summary>One (indicator, currency) → provider mapping, before it becomes a row.</summary>
    public sealed record IndicatorSourceSeed(
        string IndicatorCode,
        string Currency,
        DataSource Provider,
        string ProviderSeriesId,
        SeriesTransform Transform,
        SyncCadence Cadence);

    /// <summary>
    /// Every non-USD (indicator, currency) → provider mapping — the C3b scope.
    ///
    /// Three providers rather than seven national statistics agencies. Each was
    /// checked against the live API before being written here, which is what
    /// kept three dead integrations out of the codebase:
    /// <list type="bullet">
    /// <item>ECB <c>ICP</c> — discontinued February 2026</item>
    /// <item>Eurostat <c>prc_hicp_manr</c> — archived, ends 2025-12</item>
    /// <item>OECD <c>DF_QNA</c> (GDP) — ends 2024-Q1 for every area needed</item>
    /// </list>
    ///
    /// Gaps left on fixtures, deliberately and visibly: JPY CPI (the OECD's
    /// Japanese series ends 2021-06 and e-Stat requires a registered key), plus
    /// GDP, PMI, employment and retail for all seven. PMI is licensed everywhere,
    /// exactly as it is for USD.
    /// </summary>
    public static readonly IndicatorSourceSeed[] ReleaseSources =
    [
        // ── Policy rates: BIS WS_CBPOL, one dataflow for every central bank ──
        // Monthly, end of period, already a level, so no transform. The heaviest
        // factor in the Forex profile (weight 31) made real for all seven
        // currencies by a single integration.
        new("POLICY_RATE", "EUR", DataSource.Bis, "XM", SeriesTransform.Level, SyncCadence.Monthly),
        new("POLICY_RATE", "GBP", DataSource.Bis, "GB", SeriesTransform.Level, SyncCadence.Monthly),
        new("POLICY_RATE", "JPY", DataSource.Bis, "JP", SeriesTransform.Level, SyncCadence.Monthly),
        new("POLICY_RATE", "AUD", DataSource.Bis, "AU", SeriesTransform.Level, SyncCadence.Monthly),
        new("POLICY_RATE", "CHF", DataSource.Bis, "CH", SeriesTransform.Level, SyncCadence.Monthly),
        new("POLICY_RATE", "CAD", DataSource.Bis, "CA", SeriesTransform.Level, SyncCadence.Monthly),
        new("POLICY_RATE", "NZD", DataSource.Bis, "NZ", SeriesTransform.Level, SyncCadence.Monthly),

        // ── CPI: split by which provider is actually current ──
        // Eurostat's ECOICOP ver.2 dataset carries the euro area and Switzerland;
        // the OECD's national-CPI series carry the other four. Neither covers all
        // six, which is the only reason there are two clients here.
        //
        // EA21, not EA20: the euro area became 21 members and EA20 is now a
        // legacy aggregate. It still publishes today, but so did prc_hicp_manr
        // right up until it did not.
        new("CPI_YOY", "EUR", DataSource.Eurostat, "hicp:EA21", SeriesTransform.Level, SyncCadence.Monthly),
        new("CPI_YOY", "CHF", DataSource.Eurostat, "hicp:CH", SeriesTransform.Level, SyncCadence.Monthly),
        new("CPI_YOY", "GBP", DataSource.Oecd, "cpi:GBR.M", SeriesTransform.Level, SyncCadence.Monthly),
        new("CPI_YOY", "AUD", DataSource.Oecd, "cpi:AUS.M", SeriesTransform.Level, SyncCadence.Monthly),
        new("CPI_YOY", "CAD", DataSource.Oecd, "cpi:CAN.M", SeriesTransform.Level, SyncCadence.Monthly),
        // New Zealand publishes CPI quarterly — hence the Q key and the cadence.
        new("CPI_YOY", "NZD", DataSource.Oecd, "cpi:NZL.Q", SeriesTransform.Level, SyncCadence.Quarterly),

        // ── Labour: harmonised unemployment rate ──
        // The rate, not payrolls. Payrolls carry their signal in the surprise
        // against consensus, which no free provider publishes — the same wall
        // that forced engine v2.0.0. The rate is comparable across countries,
        // monthly everywhere, and inverted (higher unemployment weakens the
        // currency) by the indicator's own CurrencyDirection.
        //
        // Japan is current here even though its OECD CPI died in 2021, which is
        // what makes this the single largest gain for USDJPY.
        new("UNEMPLOYMENT", "EUR", DataSource.Eurostat, "unemployment:EA21", SeriesTransform.Level, SyncCadence.Monthly),
        new("UNEMPLOYMENT", "CHF", DataSource.Eurostat, "unemployment:CH", SeriesTransform.Level, SyncCadence.Monthly),
        new("UNEMPLOYMENT", "GBP", DataSource.Oecd, "unemployment:GBR", SeriesTransform.Level, SyncCadence.Monthly),
        new("UNEMPLOYMENT", "JPY", DataSource.Oecd, "unemployment:JPN", SeriesTransform.Level, SyncCadence.Monthly),
        new("UNEMPLOYMENT", "AUD", DataSource.Oecd, "unemployment:AUS", SeriesTransform.Level, SyncCadence.Monthly),
        new("UNEMPLOYMENT", "CAD", DataSource.Oecd, "unemployment:CAN", SeriesTransform.Level, SyncCadence.Monthly),
        new("UNEMPLOYMENT", "USD", DataSource.Oecd, "unemployment:USA", SeriesTransform.Level, SyncCadence.Monthly)

        // NZD is absent on purpose: New Zealand publishes labour force data
        // quarterly only, and a quarterly series cannot reach the 12 readings the
        // level component needs inside the 500-day load window. It would score on
        // direction alone — see the GDP reasoning in docs/architecture.md.
    ];

    /// <summary>
    /// Cadence is a property of the SOURCE, not of the transform: POLICY_RATE is
    /// resampled to month-end for scoring but FRED republishes DFEDTARU every
    /// business day, so it is polled daily. DXY is the inverse and the reason
    /// this distinction exists: daily observations, weekly publication.
    /// </summary>
    private static readonly Dictionary<string, SyncCadence> UsdCadence = new(StringComparer.Ordinal)
    {
        ["POLICY_RATE"] = SyncCadence.Daily,
        ["CPI_YOY"] = SyncCadence.Monthly,
        ["NFP"] = SyncCadence.Monthly,
        ["RETAIL_MOM"] = SyncCadence.Monthly,
        ["PMI_MFG"] = SyncCadence.Monthly,
        ["GDP_QOQ"] = SyncCadence.Quarterly
    };

    /// <summary>The C3a USD mappings, expressed in the same shape as the C3b ones.</summary>
    public static IEnumerable<IndicatorSourceSeed> UsdReleaseSources() =>
        UsdProviderMap.Select(kv => new IndicatorSourceSeed(
            kv.Key, "USD", DataSource.Fred, kv.Value.Series, kv.Value.Transform,
            UsdCadence.TryGetValue(kv.Key, out var cadence) ? cadence : SyncCadence.Monthly));

    /// <summary>Every provider-backed release source, USD and non-USD alike.</summary>
    public static IEnumerable<IndicatorSourceSeed> AllReleaseSources() =>
        UsdReleaseSources().Concat(ReleaseSources);

    public static List<Asset> Assets() =>
    [
        A("XAUUSD", Market.Metals, 1, L("Алт", "Gold"), ("USD", -1)),
        A("EURUSD", Market.Forex, 2, L("Евро / АНУ доллар", "Euro / US Dollar"), ("EUR", 1), ("USD", -1)),
        A("GBPUSD", Market.Forex, 3, L("Фунт / АНУ доллар", "British Pound / US Dollar"), ("GBP", 1), ("USD", -1)),
        A("USDJPY", Market.Forex, 4, L("АНУ доллар / Иен", "US Dollar / Japanese Yen"), ("USD", 1), ("JPY", -1)),
        A("AUDUSD", Market.Forex, 5, L("Австрали доллар / АНУ доллар", "Australian Dollar / US Dollar"), ("AUD", 1), ("USD", -1)),
        A("USDCHF", Market.Forex, 6, L("АНУ доллар / Швейцар франк", "US Dollar / Swiss Franc"), ("USD", 1), ("CHF", -1)),
        A("USDCAD", Market.Forex, 7, L("АНУ доллар / Канад доллар", "US Dollar / Canadian Dollar"), ("USD", 1), ("CAD", -1)),
        A("NZDUSD", Market.Forex, 8, L("Шинэ Зеланд доллар / АНУ доллар", "New Zealand Dollar / US Dollar"), ("NZD", 1), ("USD", -1)),
        A("DXY", Market.DollarIndex, 9, L("АНУ долларын индекс", "US Dollar Index"), ("USD", 1)),
        // Exposure here means "which economy drives this", not "quote currency" —
        // the Index profile's polarity carries the sign.
        A("NASDAQ", Market.Indices, 10, L("Nasdaq 100", "Nasdaq 100"), ("USD", 1))
    ];

    private static Asset A(string symbol, Market market, int order, LocalizedText name,
        params (string Currency, short Direction)[] exposures)
    {
        var asset = new Asset
        {
            Id = Id($"asset:{symbol}"),
            Symbol = symbol,
            Market = market,
            DisplayOrder = order,
            Name = name,
            IsActive = true
        };

        foreach (var (currency, direction) in exposures)
        {
            asset.Exposures.Add(new AssetCurrencyExposure
            {
                Id = Id($"exposure:{symbol}:{currency}"),
                AssetId = asset.Id,
                CurrencyCode = currency,
                Direction = direction
            });
        }

        return asset;
    }

    public static List<MarketSeries> Series() =>
    [
        new()
        {
            Id = Id("series:DXY"),
            Code = "DXY",
            FactorCode = FactorCodes.Dxy,
            Unit = IndicatorUnit.Index,
            Frequency = SeriesFrequency.Daily,
            // Fed H.10 ships weekly however often DTWEXBGS is observed.
            Cadence = SyncCadence.Weekly,
            // 10, not 5: observations are daily but the Fed's H.10 release ships
            // WEEKLY, so this series is routinely 5-7 days behind through no
            // fault of the pipeline. Staleness must be set from publication
            // cadence, not observation frequency.
            MaxAgeDays = 10,
            // Fed broad trade-weighted dollar. A different base than the ICE
            // index, which does not matter: percentile normalization is
            // scale-invariant, so swapping the synthetic series for this one
            // changes the label but not the score.
            ProviderSeriesId = "DTWEXBGS",
            // Named for what it actually is. "US Dollar Index" alone reads as the
            // ICE DXY, which sits near 99 while this sits near 120 — the score is
            // identical either way, but the number is not, and a reader comparing
            // it to their own chart would conclude the data is broken.
            Name = L("АНУ долларын өргөн индекс (Fed)", "US Dollar Index (Fed broad)"),
            ScaleNote = L("Fed өргөн", "Fed broad"),
            Description = L(
                "Холбооны нөөцийн банкны худалдаагаар жинлэсэн өргөн долларын индекс, 2006 он = 100. Арилжааны платформ дээрх ICE DXY (~99) нь өөр сагс, өөр суурьтай тул тоо нь таарахгүй. Оноо нь хувийн зэргээр тооцогддог тул масштабаас хамаарахгүй.",
                "The Federal Reserve's broad trade-weighted dollar index, 2006 = 100. The ICE DXY on a trading platform (~99) uses a different basket and base, so the numbers do not match. Scoring is by percentile and therefore scale-invariant.")
        },
        new()
        {
            Id = Id("series:US10Y_REAL"),
            Code = "US10Y_REAL",
            FactorCode = FactorCodes.Yield,
            Unit = IndicatorUnit.Percent,
            Frequency = SeriesFrequency.Daily,
            Cadence = SyncCadence.Daily,
            MaxAgeDays = 5,
            // 10Y TIPS yield: FRED publishes real yield directly, which removes
            // the nominal-minus-breakeven derivation from scoring-spec.md §4.3.
            ProviderSeriesId = "DFII10",
            Name = L("АНУ-ын 10 жилийн бодит өгөөж", "US 10Y Real Yield"),
            Description = L("Импортын үед тооцоолж хадгална.", "Computed at ingestion and stored.")
        }
    ];
}
