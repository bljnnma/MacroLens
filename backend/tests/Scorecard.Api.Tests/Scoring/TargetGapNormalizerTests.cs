using Scorecard.Api.Scoring.Normalization;
using Xunit;

namespace Scorecard.Api.Tests.Scoring;

/// <summary>
/// The v2.1.0 inflation rule, stated as cases a trader would recognise.
///
/// Sign convention under test: above target is POSITIVE for the currency,
/// because the central bank is pushed toward tightening. Getting this backwards
/// would invert the second-heaviest factor in every profile.
/// </summary>
public class TargetGapNormalizerTests
{
    private const decimal Target = 2.0m;
    private const decimal Tolerance = 0.5m;

    private static short Score(decimal current, decimal? previous) =>
        TargetGapNormalizer.Evaluate(current, previous, Target, Tolerance);

    [Fact]
    public void Inflation_at_target_and_steady_is_neutral()
    {
        Assert.Equal(0, Score(2.0m, 2.0m));
    }

    [Fact]
    public void Inside_the_tolerance_band_counts_as_on_target()
    {
        // 2.4 against a 2.0 target with 0.5 tolerance is not an overshoot; the
        // level is neutral and only the direction speaks.
        Assert.Equal(0, Score(2.4m, 2.4m));
    }

    /// <summary>
    /// The case that matters most: above target AND still rising is the strongest
    /// hawkish reading a currency can post.
    /// </summary>
    [Fact]
    public void Above_target_and_diverging_is_maximally_hawkish()
    {
        Assert.Equal(2, Score(3.5m, 3.0m));
    }

    /// <summary>
    /// The USD case from live data: 3.5% against a 2% target is a clear
    /// overshoot, but coming down from 4.2% means the bank is winning. Those
    /// cancel to neutral, which is the honest reading — and precisely what an
    /// own-history percentile could not express.
    /// </summary>
    [Fact]
    public void Above_target_but_converging_is_neutral()
    {
        Assert.Equal(0, Score(3.5m, 4.2m));
    }

    [Fact]
    public void Below_target_and_falling_further_is_maximally_dovish()
    {
        Assert.Equal(-2, Score(0.5m, 1.0m));
    }

    [Fact]
    public void Below_target_but_recovering_is_neutral()
    {
        Assert.Equal(0, Score(1.0m, 0.5m));
    }

    /// <summary>
    /// Direction is measured on the GAP, not the raw change. Inflation rising
    /// from 1.0 to 1.5 against a 2% target is the gap CLOSING — disinflation
    /// easing — while rising from 2.5 to 3.0 is the gap widening. Scoring the raw
    /// change would call both hawkish.
    /// </summary>
    [Fact]
    public void Rising_inflation_below_target_is_not_read_as_hawkish()
    {
        var belowTargetRising = Score(1.5m, 1.0m);
        var aboveTargetRising = Score(3.0m, 2.5m);

        Assert.True(belowTargetRising > -2, "closing an undershoot must soften the dovish read");
        Assert.Equal(2, aboveTargetRising);
        Assert.True(belowTargetRising < aboveTargetRising);
    }

    [Fact]
    public void A_first_reading_with_no_prior_scores_on_level_alone()
    {
        Assert.Equal(1, Score(3.0m, null));
        Assert.Equal(-1, Score(1.0m, null));
        Assert.Equal(0, Score(2.0m, null));
    }

    /// <summary>
    /// A wider mandate tolerates more. The same 2.8% print is an overshoot for
    /// the ECB's tight band and on target for the Bank of Canada's 1–3%.
    /// </summary>
    [Fact]
    public void The_tolerance_band_is_per_currency()
    {
        var tightBand = TargetGapNormalizer.Evaluate(2.8m, 2.8m, 2.0m, 0.5m);
        var wideBand = TargetGapNormalizer.Evaluate(2.8m, 2.8m, 2.0m, 1.0m);

        Assert.Equal(1, tightBand);
        Assert.Equal(0, wideBand);
    }

    /// <summary>
    /// Switzerland's mandate is "below 2%", scored as a 1.0 midpoint. A 0.7%
    /// print is on target there and a deep undershoot against a 2% target.
    /// </summary>
    [Fact]
    public void A_lower_target_changes_the_verdict_on_the_same_print()
    {
        var swiss = TargetGapNormalizer.Evaluate(0.7m, 0.7m, 1.0m, 1.0m);
        var euroArea = TargetGapNormalizer.Evaluate(0.7m, 0.7m, 2.0m, 0.5m);

        Assert.Equal(0, swiss);
        Assert.Equal(-1, euroArea);
    }

    [Theory]
    [InlineData(50.0)]
    [InlineData(-50.0)]
    public void Extreme_readings_stay_inside_the_normalized_range(double current)
    {
        var score = Score((decimal)current, 0m);

        Assert.InRange(score, (short)-2, (short)2);
    }
}
