using Scorecard.Api.Scoring.Normalization;
using Xunit;
using Xunit.Abstractions;

namespace Scorecard.Api.Tests.Scoring;

/// <summary>
/// Not an assertion — a reporter. Run it to read off the fixture's current
/// output when the engine definition changes, so goldens are transcribed rather
/// than guessed.
/// </summary>
public class PrintGoldens(ITestOutputHelper output)
{
    [Fact]
    public void Report()
    {
        foreach (var (symbol, result) in Fixture.ScoreAll())
        {
            output.WriteLine(
                $"[InlineData(\"{symbol}\", {result.Score})]  // {result.Bias}, cov {result.Coverage}");
        }

        output.WriteLine("");
        output.WriteLine($"MinimumHistory = {LevelTrendNormalizer.MinimumHistory}");

        var gold = Fixture.Score("XAUUSD");
        foreach (var f in gold.Factors)
        {
            output.WriteLine($"  XAUUSD {f.FactorCode,-7} n={f.Normalized,-4} c={f.Contribution,6}  {f.RawLabelEn}");
        }
    }
}
