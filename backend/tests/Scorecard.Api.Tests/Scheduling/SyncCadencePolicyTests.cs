using Scorecard.Api.Domain;
using Scorecard.Api.Features.Ingestion;
using Scorecard.Api.Infrastructure.Database.Seed;
using Scorecard.Api.Scoring;
using Xunit;

namespace Scorecard.Api.Tests.Scheduling;

/// <summary>
/// The policy is a pure static, so the entire scheduling contract is testable
/// without a clock, a database or a network — the same property that makes the
/// scoring contributors testable.
/// </summary>
public class SyncCadencePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The core claim of per-series scheduling: we poll far more often than the
    /// source publishes, because the point is to NOTICE the print. A check
    /// interval at or above the publication cadence would surface a monthly
    /// release weeks late.
    /// </summary>
    [Theory]
    [InlineData(SyncCadence.Daily)]
    [InlineData(SyncCadence.Weekly)]
    [InlineData(SyncCadence.Monthly)]
    [InlineData(SyncCadence.Quarterly)]
    public void Check_interval_is_always_shorter_than_the_overdue_threshold(SyncCadence cadence)
    {
        Assert.True(SyncCadencePolicy.CheckInterval(cadence) < SyncCadencePolicy.OverdueAfter(cadence));
    }

    [Fact]
    public void Rarer_cadences_tolerate_longer_silence()
    {
        var daily = SyncCadencePolicy.OverdueAfter(SyncCadence.Daily);
        var weekly = SyncCadencePolicy.OverdueAfter(SyncCadence.Weekly);
        var monthly = SyncCadencePolicy.OverdueAfter(SyncCadence.Monthly);
        var quarterly = SyncCadencePolicy.OverdueAfter(SyncCadence.Quarterly);

        Assert.True(daily < weekly);
        Assert.True(weekly < monthly);
        Assert.True(monthly < quarterly);
    }

    [Fact]
    public void Success_schedules_the_normal_check_interval()
    {
        var next = SyncCadencePolicy.NextDue(SyncCadence.Monthly, Now, consecutiveFailures: 0);

        Assert.Equal(Now + SyncCadencePolicy.CheckInterval(SyncCadence.Monthly), next);
    }

    /// <summary>
    /// A failure must be retried SOONER than the normal rhythm, not later —
    /// otherwise a transient provider blip costs a full interval of freshness.
    /// </summary>
    [Fact]
    public void First_failure_retries_sooner_than_the_normal_interval()
    {
        var normal = SyncCadencePolicy.NextDue(SyncCadence.Daily, Now, 0);
        var failed = SyncCadencePolicy.NextDue(SyncCadence.Daily, Now, 1);

        Assert.True(failed < normal);
        Assert.Equal(Now + SyncCadencePolicy.BaseBackoff, failed);
    }

    [Fact]
    public void Backoff_doubles_then_stops_at_the_check_interval()
    {
        var ceiling = SyncCadencePolicy.CheckInterval(SyncCadence.Daily);

        Assert.Equal(TimeSpan.FromMinutes(5), SyncCadencePolicy.Backoff(SyncCadence.Daily, 1));
        Assert.Equal(TimeSpan.FromMinutes(10), SyncCadencePolicy.Backoff(SyncCadence.Daily, 2));
        Assert.Equal(TimeSpan.FromMinutes(20), SyncCadencePolicy.Backoff(SyncCadence.Daily, 3));

        // Capped, not unbounded: a source failing all day must still be retried
        // on its ordinary rhythm once the provider recovers.
        Assert.Equal(ceiling, SyncCadencePolicy.Backoff(SyncCadence.Daily, 20));
    }

    [Fact]
    public void Backoff_never_overflows_on_a_long_outage()
    {
        var backoff = SyncCadencePolicy.Backoff(SyncCadence.Quarterly, int.MaxValue);

        Assert.True(backoff > TimeSpan.Zero);
        Assert.Equal(SyncCadencePolicy.CheckInterval(SyncCadence.Quarterly), backoff);
    }

    /// <summary>
    /// A schedule that has never run has nothing to be late against. Reporting
    /// overdue on a fresh install would make the first thing an operator sees a
    /// false alarm.
    /// </summary>
    [Fact]
    public void A_schedule_that_has_never_run_is_not_overdue()
    {
        var schedule = Schedule(SyncCadence.Daily);

        Assert.False(SyncCadencePolicy.IsOverdue(schedule, Now));
    }

    [Fact]
    public void Overdue_is_measured_from_the_last_change_not_the_last_success()
    {
        // A monthly source answers successfully sixty times between prints. Only
        // the gap since the last CHANGE says whether the feed has gone quiet.
        var schedule = Schedule(SyncCadence.Monthly);
        schedule.LastSuccessAt = Now;
        schedule.LastChangeAt = Now.AddDays(-50);

        Assert.True(SyncCadencePolicy.IsOverdue(schedule, Now));
    }

    [Fact]
    public void A_source_within_its_cadence_is_not_overdue()
    {
        var schedule = Schedule(SyncCadence.Monthly);
        schedule.LastSuccessAt = Now;
        schedule.LastChangeAt = Now.AddDays(-30);

        Assert.False(SyncCadencePolicy.IsOverdue(schedule, Now));
    }

    [Fact]
    public void A_disabled_schedule_is_never_overdue()
    {
        var schedule = Schedule(SyncCadence.Daily);
        schedule.IsEnabled = false;
        schedule.LastChangeAt = Now.AddYears(-1);

        Assert.False(SyncCadencePolicy.IsOverdue(schedule, Now));
    }

    /// <summary>
    /// The C3a lesson, pinned as a test: DTWEXBGS is observed daily but the Fed
    /// publishes H.10 weekly, and treating the two as one number is what
    /// collapsed DXY coverage. Cadence is publication, not observation.
    /// </summary>
    [Fact]
    public void Dxy_is_scheduled_on_its_publication_cadence_not_its_observation_frequency()
    {
        var series = SeedData.Series().ToDictionary(s => s.Code);

        Assert.Equal(SyncCadence.Weekly, series["DXY"].Cadence);
        Assert.Equal(SyncCadence.Daily, series["US10Y_REAL"].Cadence);
    }

    /// <summary>
    /// The mirror case: POLICY_RATE is resampled to month-end for scoring, but
    /// FRED republishes DFEDTARU every business day. Cadence follows the source,
    /// not the transform.
    /// </summary>
    [Theory]
    [InlineData("POLICY_RATE", SyncCadence.Daily)]
    [InlineData("CPI_YOY", SyncCadence.Monthly)]
    [InlineData("RETAIL_MOM", SyncCadence.Monthly)]
    [InlineData("PMI_MFG", SyncCadence.Monthly)]
    [InlineData("GDP_QOQ", SyncCadence.Quarterly)]
    public void Usd_indicator_cadences_match_their_provider_publication(string code, SyncCadence expected)
    {
        var source = SeedData.UsdReleaseSources().Single(s => s.IndicatorCode == code);

        Assert.Equal(expected, source.Cadence);
    }

    /// <summary>
    /// The mirror of the POLICY_RATE case across the C3b providers: the BIS
    /// republishes every policy rate monthly whatever each central bank's own
    /// meeting schedule looks like, and New Zealand's quarterly CPI has to be
    /// polled on a quarterly rhythm rather than the monthly one its five peers use.
    /// </summary>
    [Theory]
    [InlineData("POLICY_RATE", "EUR", SyncCadence.Monthly)]
    [InlineData("POLICY_RATE", "JPY", SyncCadence.Monthly)]
    [InlineData("CPI_YOY", "EUR", SyncCadence.Monthly)]
    [InlineData("CPI_YOY", "GBP", SyncCadence.Monthly)]
    [InlineData("CPI_YOY", "NZD", SyncCadence.Quarterly)]
    public void Non_usd_cadences_follow_each_provider(string code, string currency, SyncCadence expected)
    {
        var source = SeedData.ReleaseSources
            .Single(s => s.IndicatorCode == code && s.Currency == currency);

        Assert.Equal(expected, source.Cadence);
    }

    /// <summary>
    /// Every factor a profile weights must have a contributor, or it silently
    /// never scores — a weight row pointing at nothing looks identical to a
    /// factor whose data is missing.
    ///
    /// This also guards the failure that hit the dashboard snapshot: it held a
    /// hand-written contributor list that went stale the moment profile v2
    /// retired four factors, so "strongest currency" was still averaging GDP and
    /// PMI while no score used them.
    /// </summary>
    [Fact]
    public void Every_weighted_factor_has_a_contributor()
    {
        var implemented = typeof(IScoreContributor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IScoreContributor).IsAssignableFrom(t))
            .Select(t => ((IScoreContributor)Activator.CreateInstance(t)!).FactorCode)
            .ToHashSet(StringComparer.Ordinal);

        var weighted = SeedData.Profiles(DateTimeOffset.UtcNow)
            .SelectMany(p => p.Weights.Where(w => w.IsEnabled && w.Weight > 0m))
            .Select(w => w.FactorCode)
            .Distinct();

        foreach (var code in weighted)
            Assert.True(implemented.Contains(code), $"profile weights {code} but no contributor implements it");
    }

    /// <summary>
    /// Every provider mapping must name an indicator that exists. A typo here
    /// would silently drop a currency from ingestion — the seeder logs and skips
    /// rather than failing, which is right at runtime and wrong in a test.
    /// </summary>
    [Fact]
    public void Every_release_source_maps_to_a_real_indicator()
    {
        var indicators = SeedData.Indicators().Select(i => i.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var source in SeedData.AllReleaseSources())
        {
            Assert.True(
                indicators.Contains(source.IndicatorCode),
                $"Mapping for {source.IndicatorCode}/{source.Currency} names an unknown indicator.");
        }
    }

    /// <summary>
    /// One provider per (indicator, currency). A duplicate would be rejected by
    /// the unique index at startup, but only after a confusing partial seed.
    /// </summary>
    [Fact]
    public void Release_sources_are_unique_per_indicator_and_currency()
    {
        var duplicates = SeedData.AllReleaseSources()
            .GroupBy(s => (s.IndicatorCode, s.Currency))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.IndicatorCode}/{g.Key.Currency}")
            .ToList();

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Two indicators feeding one factor for the same currency makes the loader's
    /// choice arbitrary — with equal periods the tiebreak between them is
    /// undefined. USD carried exactly this collision when payrolls and the
    /// unemployment rate both fed LABOUR.
    /// </summary>
    [Fact]
    public void No_factor_is_fed_by_two_indicators_for_the_same_currency()
    {
        var factorByIndicator = SeedData.Indicators()
            .ToDictionary(i => i.Code, i => i.FactorCode, StringComparer.Ordinal);

        var collisions = SeedData.AllReleaseSources()
            .Where(s => factorByIndicator.ContainsKey(s.IndicatorCode))
            .GroupBy(s => (Factor: factorByIndicator[s.IndicatorCode], s.Currency))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Factor}/{g.Key.Currency}: {string.Join(" + ", g.Select(x => x.IndicatorCode))}")
            .ToList();

        Assert.Empty(collisions);
    }

    /// <summary>
    /// The labour integration, pinned. Six currencies plus USD score on the same
    /// harmonised unemployment rate; NZD is absent because New Zealand publishes
    /// only quarterly, which cannot reach the 12 readings the level component
    /// needs inside the load window.
    /// </summary>
    [Fact]
    public void Labour_covers_every_currency_except_new_zealand()
    {
        var labour = SeedData.AllReleaseSources()
            .Where(s => s.IndicatorCode == "UNEMPLOYMENT")
            .Select(s => s.Currency)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("NZD", labour);
        foreach (var currency in SeedData.Currencies.Where(c => c != "NZD"))
            Assert.Contains(currency, labour);
    }

    /// <summary>
    /// Payrolls are deliberately not a scoring source. Their signal lives in the
    /// surprise against consensus, which no free provider publishes — the same
    /// wall that forced engine v2.0.0.
    /// </summary>
    [Fact]
    public void Payrolls_are_not_mapped_as_a_scoring_source()
    {
        Assert.DoesNotContain(SeedData.AllReleaseSources(), s => s.IndicatorCode == "NFP");
    }

    /// <summary>
    /// The C3b scope, pinned: policy rates real for all seven non-USD currencies,
    /// CPI real for six. JPY CPI is a known gap — the OECD's Japanese series ends
    /// 2021-06 — and this test fails if someone quietly maps it to a stale source.
    /// </summary>
    [Fact]
    public void C3b_covers_every_non_usd_policy_rate_and_all_cpi_except_jpy()
    {
        var currencies = SeedData.Currencies.Where(c => c != "USD").ToList();

        var rates = SeedData.ReleaseSources
            .Where(s => s.IndicatorCode == "POLICY_RATE")
            .Select(s => s.Currency)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(currencies.Count, rates.Count);
        foreach (var currency in currencies)
            Assert.Contains(currency, rates);

        var cpi = SeedData.ReleaseSources
            .Where(s => s.IndicatorCode == "CPI_YOY")
            .Select(s => s.Currency)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("JPY", cpi);
        foreach (var currency in currencies.Where(c => c != "JPY"))
            Assert.Contains(currency, cpi);
    }

    private static SyncSchedule Schedule(SyncCadence cadence) => new()
    {
        Id = Guid.NewGuid(),
        SourceKind = SyncSourceKind.Series,
        SourceCode = "TEST",
        Cadence = cadence,
        IsEnabled = true,
        NextDueAt = Now
    };
}
