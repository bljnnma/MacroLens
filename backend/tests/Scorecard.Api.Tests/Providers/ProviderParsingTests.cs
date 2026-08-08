using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Providers;
using Xunit;

namespace Scorecard.Api.Tests.Providers;

/// <summary>
/// Parsing rules for the C3b providers, exercised against the exact response
/// shapes the live APIs returned during reconnaissance. Fixtures rather than
/// network calls: the tests must fail on a parsing regression, not on the BIS
/// being slow.
/// </summary>
public class ProviderParsingTests
{
    /// <summary>
    /// The reason SdmxCsv exists. BIS embeds commas inside a quoted TITLE, and a
    /// naive Split(',') shifts every later column — reading a description where
    /// the value should be, with no error to notice.
    /// </summary>
    [Fact]
    public void Bis_csv_survives_commas_inside_quoted_fields()
    {
        const string body = """
            FREQ,REF_AREA,TITLE,TIME_PERIOD,OBS_VALUE
            M,GB,"From 3 Aug 2006 onwards: official bank rate; from 6 May 1997, repo rate.",2026-06,3.75
            M,GB,"From 3 Aug 2006 onwards: official bank rate; from 6 May 1997, repo rate.",2026-07,3.50
            """;

        var result = BisClient.ParseCsv(body);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 6, 1), result[0].Date);
        Assert.Equal(3.75m, result[0].Value);
        Assert.Equal(3.50m, result[1].Value);
    }

    [Fact]
    public void Bis_csv_returns_readings_oldest_first()
    {
        const string body = """
            FREQ,REF_AREA,TIME_PERIOD,OBS_VALUE
            M,XM,2026-07,2.25
            M,XM,2026-05,2.50
            M,XM,2026-06,2.25
            """;

        var result = BisClient.ParseCsv(body);

        Assert.Equal(
            [new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1)],
            result.Select(r => r.Date));
    }

    [Fact]
    public void Bis_csv_ignores_rows_with_an_unparseable_value()
    {
        const string body = """
            FREQ,REF_AREA,TIME_PERIOD,OBS_VALUE
            M,JP,2026-06,NA
            M,JP,2026-07,1
            """;

        var result = BisClient.ParseCsv(body);

        Assert.Single(result);
        Assert.Equal(1m, result[0].Value);
    }

    [Fact]
    public void Bis_csv_handles_an_empty_body()
    {
        Assert.Empty(BisClient.ParseCsv(string.Empty));
        Assert.Empty(BisClient.ParseCsv("FREQ,REF_AREA,TIME_PERIOD,OBS_VALUE"));
    }

    /// <summary>
    /// Eurostat's value map is sparse: a country that has not published a month
    /// yet simply has no key. Switzerland trails the euro area by one month for
    /// exactly this reason, and reading the gap as zero would drag the level
    /// percentile down.
    /// </summary>
    [Fact]
    public void Eurostat_json_skips_periods_with_no_published_value()
    {
        const string body = """
            {"value":{"0":2.1,"1":2.4},
             "dimension":{"time":{"category":{"index":{"2026-05":0,"2026-06":1,"2026-07":2}}}}}
            """;

        var result = EurostatClient.ParseJsonStat(body);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 5, 1), result[0].Date);
        Assert.Equal(2.4m, result[1].Value);
    }

    [Fact]
    public void Eurostat_json_resolves_values_by_index_not_by_order()
    {
        // The index map is deliberately out of order here: relying on JSON
        // property order would pair the wrong value with the wrong month.
        const string body = """
            {"value":{"2":3.0,"0":1.0,"1":2.0},
             "dimension":{"time":{"category":{"index":{"2026-07":2,"2026-05":0,"2026-06":1}}}}}
            """;

        var result = EurostatClient.ParseJsonStat(body);

        Assert.Equal(
            [(new DateOnly(2026, 5, 1), 1.0m), (new DateOnly(2026, 6, 1), 2.0m), (new DateOnly(2026, 7, 1), 3.0m)],
            result.Select(r => (r.Date, r.Value)));
    }

    [Fact]
    public void Eurostat_json_treats_an_error_envelope_as_no_data()
    {
        const string body = """{"error":[{"status":400,"label":"INVALID_QUERY_DIMENSION"}]}""";

        Assert.Empty(EurostatClient.ParseJsonStat(body));
    }

    [Fact]
    public void Eurostat_json_skips_explicit_nulls()
    {
        const string body = """
            {"value":{"0":null,"1":2.4},
             "dimension":{"time":{"category":{"index":{"2026-06":0,"2026-07":1}}}}}
            """;

        var result = EurostatClient.ParseJsonStat(body);

        Assert.Single(result);
        Assert.Equal(2.4m, result[0].Value);
    }

    /// <summary>
    /// Every period shape maps to the FIRST day of the period. New Zealand's
    /// quarterly CPI shares one indicator with five monthly reporters, so the two
    /// have to land on a comparable Period or the release ordering is wrong.
    /// </summary>
    [Theory]
    [InlineData("2026-07", 2026, 7, 1)]
    [InlineData("2026-Q1", 2026, 1, 1)]
    [InlineData("2026-Q2", 2026, 4, 1)]
    [InlineData("2026-Q3", 2026, 7, 1)]
    [InlineData("2026-Q4", 2026, 10, 1)]
    [InlineData("2026-07-31", 2026, 7, 31)]
    [InlineData("2026", 2026, 1, 1)]
    public void Sdmx_periods_map_to_the_start_of_the_period(string period, int y, int m, int d)
    {
        Assert.Equal(new DateOnly(y, m, d), SdmxCsv.ParsePeriod(period));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-13")]
    [InlineData("2026-Q5")]
    [InlineData("not-a-date")]
    public void Unparseable_periods_are_rejected_rather_than_guessed(string period)
    {
        Assert.Null(SdmxCsv.ParsePeriod(period));
    }

    /// <summary>
    /// A reading covering June cannot have been published before 30 June, so the
    /// period END is the honest stamp. The START back-dates a monthly figure by
    /// a month and a quarterly one by a quarter, which is what made NZD's CPI
    /// read as four months old while being three weeks old.
    /// </summary>
    [Theory]
    [InlineData(2026, 6, 1, SyncCadence.Monthly, 2026, 6, 30)]
    [InlineData(2026, 2, 1, SyncCadence.Monthly, 2026, 2, 28)]
    [InlineData(2024, 2, 1, SyncCadence.Monthly, 2024, 2, 29)]
    [InlineData(2026, 12, 1, SyncCadence.Monthly, 2026, 12, 31)]
    [InlineData(2026, 4, 1, SyncCadence.Quarterly, 2026, 6, 30)]
    [InlineData(2026, 10, 1, SyncCadence.Quarterly, 2026, 12, 31)]
    [InlineData(2026, 6, 15, SyncCadence.Daily, 2026, 6, 15)]
    public void Period_end_is_the_last_day_the_reading_covers(
        int y, int m, int d, SyncCadence cadence, int ey, int em, int ed)
    {
        Assert.Equal(
            new DateOnly(ey, em, ed),
            SdmxCsv.PeriodEnd(new DateOnly(y, m, d), cadence));
    }

    [Fact]
    public void Period_end_never_precedes_the_period_start()
    {
        foreach (var cadence in Enum.GetValues<SyncCadence>())
        {
            var start = new DateOnly(2026, 3, 1);
            Assert.True(SdmxCsv.PeriodEnd(start, cadence) >= start);
        }
    }

    [Theory]
    [InlineData("hicp:EA21", "prc_hicp_minr", "EA21")]
    [InlineData("unemployment:CH", "une_rt_m", "CH")]
    public void Eurostat_series_ids_resolve_to_a_dataset_and_geo(
        string seriesId, string dataset, string geo)
    {
        var resolved = EurostatClient.Resolve(seriesId);

        Assert.Equal(dataset, resolved.Dataset);
        Assert.Equal(geo, resolved.Geo);
        Assert.NotEmpty(resolved.Filters);
    }

    [Fact]
    public void Oecd_cpi_and_unemployment_use_different_dataflows()
    {
        var cpi = OecdClient.Resolve("cpi:GBR.M");
        var unemployment = OecdClient.Resolve("unemployment:GBR");

        Assert.Contains("DSD_PRICES", cpi.Dataflow);
        Assert.Contains("DSD_LFS", unemployment.Dataflow);
        Assert.NotEqual(cpi.Key, unemployment.Key);
    }

    [Fact]
    public void Oecd_quarterly_cpi_is_flagged_so_the_start_period_is_formatted_correctly()
    {
        Assert.True(OecdClient.Resolve("cpi:NZL.Q").Quarterly);
        Assert.False(OecdClient.Resolve("cpi:GBR.M").Quarterly);
        Assert.False(OecdClient.Resolve("unemployment:JPN").Quarterly);
    }

    [Theory]
    [InlineData("EA21")]
    [InlineData("hicp")]
    [InlineData("nonsense:EA21")]
    public void Unknown_or_malformed_eurostat_ids_throw_rather_than_guess(string seriesId)
    {
        Assert.Throws<InvalidOperationException>(() => EurostatClient.Resolve(seriesId));
    }

    [Theory]
    [InlineData("GBR.M")]
    [InlineData("nonsense:GBR")]
    [InlineData("cpi:GBR")]
    public void Unknown_or_malformed_oecd_ids_throw_rather_than_guess(string seriesId)
    {
        Assert.Throws<InvalidOperationException>(() => OecdClient.Resolve(seriesId));
    }
}
