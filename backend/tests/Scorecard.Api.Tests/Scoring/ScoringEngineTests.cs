using Scorecard.Api.Domain;
using Scorecard.Api.Scoring;
using Scorecard.Api.Scoring.Contributors;
using Scorecard.Api.Scoring.Normalization;
using Xunit;

namespace Scorecard.Api.Tests.Scoring;

public class ScoringEngineTests
{
    /// <summary>
    /// Engine v2.2.0 / profile v2 values. The same fixture scored 91.0 under
    /// v1.0.0 (surprise-vs-consensus) and 88.4 under v2.0.0 (own-history
    /// percentile); each move is a change of definition, not a regression.
    /// Gold is unchanged by the half-step move — it has no pair differential.
    /// </summary>
    [Fact]
    public void Gold_reproduces_the_golden_fixture()
    {
        var result = Fixture.Score("XAUUSD");

        Assert.Equal(86.5m, result.Score);
        Assert.Equal(Bias.Bullish, result.Bias);
        Assert.Equal(1m, result.Coverage);
        Assert.True(result.IsSufficient);
    }

    [Theory]
    [InlineData("XAUUSD", 86.5)]
    [InlineData("NASDAQ", 77.7)]
    [InlineData("EURUSD", 76.3)]
    [InlineData("GBPUSD", 75.0)]
    [InlineData("AUDUSD", 67.1)]
    [InlineData("NZDUSD", 63.9)]
    [InlineData("USDCHF", 46.8)]
    [InlineData("USDCAD", 42.5)]
    [InlineData("DXY", 28.7)]
    [InlineData("USDJPY", 23.9)]
    public void Scores_match_the_published_fixture(string symbol, double expected)
    {
        Assert.Equal((decimal)expected, Fixture.Score(symbol).Score);
    }

    /// <summary>
    /// The level half of the level-and-direction normalizer must actually engage
    /// against the fixture — otherwise these tests would only ever cover the
    /// direction half. Labour still uses it; inflation moved to a target gap.
    /// </summary>
    [Fact]
    public void Level_component_engages_where_history_is_long_enough()
    {
        var result = Fixture.Score("EURUSD");
        var labour = result.Factors.Single(f => f.FactorCode == FactorCodes.Labour);

        Assert.Contains("percentile of", labour.RawLabelEn);
    }

    /// <summary>
    /// The percentile must see every reading the fixture generates, not just the
    /// slice that fitted inside the staleness window.
    ///
    /// The loader used to run one 500-day lookback for both purposes, which cut a
    /// monthly series to roughly sixteen points. The fixture builds 24 periods, so
    /// anything less than 24 here means the two windows have been conflated again.
    /// </summary>
    [Fact]
    public void Percentile_history_is_not_truncated_by_the_staleness_window()
    {
        var data = Fixture.Data();
        var snapshot = data.Releases[(FactorCodes.Labour, "USD")];

        Assert.True(
            snapshot.History.Count >= 24,
            $"expected the full generated history, got {snapshot.History.Count} readings");
    }

    /// <summary>
    /// A percentile without its sample size overstates its own precision — "0th
    /// percentile" means something very different from sixteen readings than from
    /// sixty, and the reader cannot tell which they are looking at.
    /// </summary>
    [Fact]
    public void Percentile_labels_state_how_many_readings_they_are_drawn_from()
    {
        var result = Fixture.Score("EURUSD");
        var labour = result.Factors.Single(f => f.FactorCode == FactorCodes.Labour);

        Assert.Matches(@"percentile of \d+ readings", labour.RawLabelEn);
        Assert.Matches(@"\d+ уншилтын", labour.RawLabelMn);
    }

    /// <summary>
    /// Inflation is read against the mandate, and the label has to say so — a
    /// percentile tells a trader nothing about what the central bank will do.
    /// </summary>
    [Fact]
    public void Inflation_is_scored_against_the_published_target()
    {
        var result = Fixture.Score("XAUUSD");
        var cpi = result.Factors.Single(f => f.FactorCode == FactorCodes.Cpi);

        Assert.Contains("target", cpi.RawLabelEn);
        Assert.DoesNotContain("percentile", cpi.RawLabelEn);
    }

    /// <summary>The documented macro scenario must survive the generated history.</summary>
    [Fact]
    public void Seeded_scenario_direction_is_preserved()
    {
        var result = Fixture.Score("XAUUSD");
        var rate = result.Factors.Single(f => f.FactorCode == FactorCodes.Rate);

        Assert.Contains("cut 25bp", rate.RawLabelEn);
    }

    /// <summary>
    /// The arithmetic a user checks by hand must be the arithmetic the engine did.
    /// </summary>
    [Fact]
    public void Base_plus_contributions_equals_score_for_every_asset()
    {
        foreach (var (symbol, result) in Fixture.ScoreAll())
        {
            var sum = result.Factors.Sum(f => f.Contribution);
            Assert.Equal(result.Score, EngineVersion.BaseScore + sum);
        }
    }

    [Fact]
    public void Bias_matches_the_profile_thresholds()
    {
        foreach (var (symbol, result) in Fixture.ScoreAll())
        {
            var expected = result.Score >= 65m
                ? Bias.Bullish
                : result.Score <= 35m ? Bias.Bearish : Bias.Neutral;
            Assert.Equal(expected, result.Bias);
        }
    }

    [Fact]
    public void Normalized_scores_stay_within_range()
    {
        foreach (var (_, result) in Fixture.ScoreAll())
        foreach (var factor in result.Factors.Where(f => f.Normalized is not null))
        {
            Assert.InRange(factor.Normalized!.Value, -2m, 2m);

            // Half steps are the finest resolution the arithmetic produces;
            // anything finer would be invented precision.
            Assert.Equal(0m, factor.Normalized.Value * 2m % 1m);
        }
    }

    [Fact]
    public void Scoring_is_deterministic()
    {
        var first = Fixture.Score("EURUSD");
        var second = Fixture.Score("EURUSD");

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(
            first.Factors.Select(f => (f.FactorCode, f.Normalized, f.Contribution)),
            second.Factors.Select(f => (f.FactorCode, f.Normalized, f.Contribution)));
    }

    /// <summary>
    /// Partial data still scores, but says so. NZD is missing a reading this
    /// cycle and lands at 0.76 — above the floor, so the number stands with a
    /// coverage figure beside it.
    /// </summary>
    [Fact]
    public void Partial_data_is_reported_with_its_coverage()
    {
        var result = Fixture.Score("NZDUSD");

        Assert.Equal(0.76m, result.Coverage);
        Assert.True(result.IsSufficient);
        Assert.True(result.Coverage < 1m);
    }

    /// <summary>
    /// Coverage is what stops a thin data set from posting a confident-looking
    /// score. Driven by letting the fixture go stale rather than by a
    /// hand-crafted sparse asset: the rule under test is that missing weight
    /// disqualifies a score, whatever made it missing.
    /// </summary>
    [Fact]
    public void Sparse_data_falls_below_the_coverage_floor()
    {
        // Far enough out that everything except the policy rate has expired —
        // rates carry a 400-day window because a rate is a standing level.
        var result = Fixture.Score("EURUSD", Fixture.AsOf.AddDays(200));

        Assert.True(result.Coverage < 0.60m, $"expected sparse coverage, got {result.Coverage}");
        Assert.False(result.IsSufficient);
    }

    [Fact]
    public void Dollar_index_is_not_scored_by_the_dollar_factor()
    {
        var result = Fixture.Score("DXY");
        Assert.DoesNotContain(result.Factors, f => f.FactorCode == FactorCodes.Dxy);
    }

    [Fact]
    public void Unavailable_factors_are_reported_rather_than_hidden()
    {
        var result = Fixture.Score("NZDUSD");
        var cpi = result.Factors.Single(f => f.FactorCode == FactorCodes.Cpi);

        Assert.Null(cpi.Normalized);
        Assert.Equal(0m, cpi.Contribution);
        Assert.Contains("coverage", cpi.ExplanationEn, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Profile v2 retired four factors. A retired factor must not appear on a
    /// score at all — a zero-weight row would still occupy a heatmap column and
    /// imply the model considered it.
    /// </summary>
    [Theory]
    [InlineData(FactorCodes.Gdp)]
    [InlineData(FactorCodes.Pmi)]
    [InlineData(FactorCodes.Retail)]
    [InlineData(FactorCodes.Cot)]
    [InlineData(FactorCodes.Nfp)]
    public void Retired_factors_are_absent_from_every_score(string factorCode)
    {
        foreach (var (symbol, result) in Fixture.ScoreAll())
            Assert.DoesNotContain(result.Factors, f => f.FactorCode == factorCode);
    }

    /// <summary>
    /// A pair's factor score is a differential, so both sides have to be
    /// recorded. Without them the cell shows the base currency's raw value
    /// beside a number derived from two currencies, and the arithmetic cannot be
    /// checked — which is the one thing the product promises.
    /// </summary>
    [Fact]
    public void A_pair_records_a_reading_for_each_side()
    {
        var result = Fixture.Score("EURUSD");
        var labour = result.Factors.Single(f => f.FactorCode == FactorCodes.Labour);

        Assert.Equal(2, labour.Readings.Count);
        Assert.Contains(labour.Readings, r => r.Currency == "EUR" && r.Direction == 1);
        Assert.Contains(labour.Readings, r => r.Currency == "USD" && r.Direction == -1);
    }

    /// <summary>
    /// The stored readings must reproduce the stored score. This is the
    /// arithmetic a reader performs by hand from the detail page.
    /// </summary>
    [Fact]
    public void The_recorded_readings_reproduce_the_differential()
    {
        var result = Fixture.Score("EURUSD");

        foreach (var factor in result.Factors.Where(f => f.Readings.Count == 2))
        {
            var b = factor.Readings.Single(r => r.Direction == 1);
            var q = factor.Readings.Single(r => r.Direction == -1);

            var expected = Rounding.ToNormalized(
                factor.Polarity * (b.Normalized - q.Normalized) / 2m);

            Assert.Equal(expected, factor.Normalized);
        }
    }

    /// <summary>
    /// The differential's own resolution must survive to the stored score.
    ///
    /// Rounding it to a whole number discarded a half step in about half of all
    /// pair cells and, going away from zero, did so with a systematic bias —
    /// every 0.5 became 1.0, pushing scores away from neutral. On live data that
    /// moved USDCHF by 9.8 points.
    /// </summary>
    [Fact]
    public void An_odd_differential_keeps_its_half_step()
    {
        foreach (var (symbol, result) in Fixture.ScoreAll())
        foreach (var factor in result.Factors.Where(f => f.Readings.Count == 2))
        {
            var b = factor.Readings.Single(r => r.Direction == 1).Normalized;
            var q = factor.Readings.Single(r => r.Direction == -1).Normalized;

            // An odd gap between two integers is exactly a half step. If the
            // stored value is whole, resolution was thrown away.
            if (Math.Abs(b - q) % 2 == 0) continue;

            Assert.True(
                factor.Normalized % 1m != 0m,
                $"{symbol} {factor.FactorCode}: {b} vs {q} should keep a half step, got {factor.Normalized}");
        }
    }

    /// <summary>
    /// The fixture has to contain at least one odd differential, or the test
    /// above passes without ever exercising the case it exists for.
    /// </summary>
    [Fact]
    public void The_fixture_exercises_at_least_one_half_step()
    {
        var halfSteps = Fixture.ScoreAll()
            .SelectMany(x => x.Result.Factors)
            .Count(f => f.Normalized is { } n && n % 1m != 0m);

        Assert.True(halfSteps > 0, "no factor scored on a half step");
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(-0.5, -0.5)]
    [InlineData(1.5, 1.5)]
    [InlineData(-1.5, -1.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(0.25, 0.5)]   // residual rounds away from zero
    [InlineData(-0.25, -0.5)]
    [InlineData(3.0, 2.0)]    // still clamped
    [InlineData(-3.0, -2.0)]
    public void Normalization_resolves_to_half_steps_within_range(double input, double expected)
    {
        Assert.Equal((decimal)expected, Rounding.ToNormalized((decimal)input));
    }

    /// <summary>
    /// USD-scoped factors bypass the differential entirely, so they carry one
    /// reading, not two — see FactorContributorBase.
    /// </summary>
    [Fact]
    public void A_usd_scoped_factor_records_a_single_reading()
    {
        var result = Fixture.Score("EURUSD");
        var dxy = result.Factors.Single(f => f.FactorCode == FactorCodes.Dxy);

        Assert.Single(dxy.Readings);
        Assert.Equal("USD", dxy.Readings[0].Currency);
    }

    [Fact]
    public void An_unavailable_factor_records_no_readings()
    {
        var result = Fixture.Score("NZDUSD");
        var cpi = result.Factors.Single(f => f.FactorCode == FactorCodes.Cpi);

        Assert.Null(cpi.Normalized);
        Assert.Empty(cpi.Readings);
    }

    /// <summary>
    /// The dollar reading is the Fed's broad index near 120, not the ICE DXY near
    /// 99 that a trader has on screen. Percentile scoring makes the SCORE
    /// identical either way, but a bare number under a dollar heading reads as
    /// broken data, so the value has to say which index it is.
    /// </summary>
    [Fact]
    public void The_dollar_reading_names_the_index_it_comes_from()
    {
        var result = Fixture.Score("XAUUSD");
        var dxy = result.Factors.Single(f => f.FactorCode == FactorCodes.Dxy);

        Assert.Contains("Fed broad", dxy.RawLabelEn);
        Assert.Contains("Fed өргөн", dxy.RawLabelMn);
    }

    /// <summary>
    /// A qualifier is only added where the number is ambiguous. A real yield in
    /// percent needs none, and appending one everywhere would be noise.
    /// </summary>
    [Fact]
    public void A_series_with_an_unambiguous_unit_carries_no_qualifier()
    {
        var result = Fixture.Score("XAUUSD");
        var yieldFactor = result.Factors.Single(f => f.FactorCode == FactorCodes.Yield);

        Assert.DoesNotContain("(", yieldFactor.RawLabelEn);
    }

    /// <summary>
    /// Real policy rate is context, not a factor: with the nominal rate and
    /// inflation both weighted it is a linear combination of the two and would
    /// only count inflation twice with opposite signs.
    /// </summary>
    [Fact]
    public void Real_policy_rate_is_not_a_scored_factor()
    {
        Assert.Equal(1.25m, TargetGapNormalizer.RealRate(3.75m, 2.50m));
        Assert.Null(TargetGapNormalizer.RealRate(3.75m, null));

        foreach (var (_, result) in Fixture.ScoreAll())
            Assert.DoesNotContain(result.Factors, f => f.FactorCode.Contains("REAL"));
    }

    /// <summary>
    /// Age is measured against the context, not the wall clock — so replaying an
    /// old context must produce the same staleness verdict.
    /// </summary>
    [Fact]
    public void Releases_past_their_max_age_become_unavailable()
    {
        var fresh = Fixture.Score("EURUSD");
        var stale = Fixture.Score("EURUSD", Fixture.AsOf.AddDays(90));

        Assert.NotNull(fresh.Factors.Single(f => f.FactorCode == FactorCodes.Cpi).Normalized);
        Assert.Null(stale.Factors.Single(f => f.FactorCode == FactorCodes.Cpi).Normalized);
        Assert.True(stale.Coverage < fresh.Coverage);
    }

    /// <summary>
    /// A scheduled row whose release time has passed but whose actual has not
    /// been ingested must not displace the last good print. Found by running
    /// against a seeded calendar that contains forward-looking events.
    /// </summary>
    [Fact]
    public void An_unprinted_release_never_displaces_the_last_printed_one()
    {
        var data = Fixture.Data();
        var gold = data.Assets.Single(a => a.Symbol == "XAUUSD");

        var cpi = data.Releases[(FactorCodes.Cpi, "USD")];
        Assert.NotNull(cpi.Release.Actual);

        // The seed contains a next-period CPI row with no actual; selecting it
        // would drop the factor and collapse coverage.
        var scored = Fixture.Score("XAUUSD");
        Assert.NotNull(scored.Factors.Single(f => f.FactorCode == FactorCodes.Cpi).Normalized);
        Assert.Equal(1m, scored.Coverage);
        Assert.Equal(gold.Symbol, "XAUUSD");
    }

    [Fact]
    public void Explanations_are_generated_in_both_languages()
    {
        var result = Fixture.Score("EURUSD");
        var rate = result.Factors.Single(f => f.FactorCode == FactorCodes.Rate);

        Assert.NotEmpty(rate.ExplanationMn);
        Assert.NotEmpty(rate.ExplanationEn);
        Assert.NotEqual(rate.ExplanationMn, rate.ExplanationEn);
        Assert.Contains("EURUSD", rate.ExplanationEn);
    }

    /// <summary>
    /// The English labels read as prose, so the ordinal has to be right — real
    /// data produced "81th percentile" on the EURUSD page. Mongolian needs no
    /// equivalent: "-р" is invariant.
    /// </summary>
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    [InlineData(81, "81st")]
    [InlineData(96, "96th")]
    [InlineData(100, "100th")]
    public void English_percentile_labels_use_the_right_ordinal(int value, string expected)
    {
        Assert.Equal(expected, Labels.Ordinal(value));
    }
}
