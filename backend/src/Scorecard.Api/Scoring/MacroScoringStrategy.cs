using Scorecard.Api.Domain;
using Scorecard.Api.Scoring.Normalization;

namespace Scorecard.Api.Scoring;

/// <summary>
/// The only concrete strategy. With both weights and contributor selection held
/// in the database, Gold versus Forex is a data difference, not a code one.
/// </summary>
public sealed class MacroScoringStrategy(IEnumerable<IScoreContributor> contributors) : IScoringStrategy
{
    private readonly IReadOnlyDictionary<string, IScoreContributor> _contributors =
        contributors.ToDictionary(c => c.FactorCode, StringComparer.Ordinal);

    public bool Handles(Market market) => true;

    public ScoreResult Score(ScoringContext context)
    {
        var profile = context.Profile;

        // The weight rows ARE the contributor selection — a factor with no
        // enabled row simply never runs.
        var enabled = profile.Weights.Where(w => w.IsEnabled).ToList();

        var evaluated = new List<(ProfileWeight Weight, FactorEvaluation? Evaluation)>();
        foreach (var weight in enabled)
        {
            _contributors.TryGetValue(weight.FactorCode, out var contributor);
            evaluated.Add((weight, contributor?.Evaluate(context, weight)));
        }

        var participating = evaluated.Where(e => e.Evaluation is not null).ToList();

        var enabledWeight = enabled.Sum(w => w.Weight);
        var participatingWeight = participating.Sum(e => e.Weight.Weight);

        // maxAbs counts participating factors only, which is exactly why coverage
        // has to be reported next to the score: an asset with two of eight
        // factors can otherwise post a confident-looking 88.
        var maxAbs = participating.Sum(e => 2m * e.Weight.Weight);
        var scale = maxAbs == 0m ? 0m : EngineVersion.BaseScore / maxAbs;

        var factors = new List<ScoredFactor>();

        foreach (var (weight, evaluation) in evaluated)
        {
            context.Factors.TryGetValue(weight.FactorCode, out var factor);

            if (evaluation is null)
            {
                factors.Add(new ScoredFactor(
                    weight.FactorCode, null, null, "—", "—",
                    weight.Weight, weight.Polarity, 0m,
                    Explanations.UnavailableMn, Explanations.UnavailableEn, []));
                continue;
            }

            var contribution = Rounding.ToContribution(evaluation.Normalized * weight.Weight * scale);

            var (mn, en) = factor is null
                ? (Explanations.UnavailableMn, Explanations.UnavailableEn)
                : Explanations.Compose(factor, context.Asset, evaluation, weight.Weight, weight.Polarity, contribution);

            factors.Add(new ScoredFactor(
                weight.FactorCode,
                evaluation.Normalized,
                evaluation.RawValue,
                evaluation.RawLabelMn,
                evaluation.RawLabelEn,
                weight.Weight,
                weight.Polarity,
                contribution,
                mn,
                en,
                // Both sides of the differential, so a pair's score can be
                // checked rather than taken on trust.
                evaluation.Readings
                    .Select(r => new FactorReading(
                        r.Currency, r.Direction, r.Reading.Score,
                        r.Reading.RawValue, r.Reading.LabelMn, r.Reading.LabelEn))
                    .ToList()));
        }

        // Summed in a fixed order over already-rounded values, so the total is
        // reproducible and 50 + Σ closes exactly.
        var sum = factors
            .OrderBy(f => f.FactorCode, StringComparer.Ordinal)
            .Sum(f => f.Contribution);

        var score = Math.Clamp(EngineVersion.BaseScore + sum, 0m, 100m);
        var coverage = enabledWeight == 0m ? 0m : Math.Round(participatingWeight / enabledWeight, 3);

        var bias = score >= profile.BullishThreshold
            ? Bias.Bullish
            : score <= profile.BearishThreshold
                ? Bias.Bearish
                : Bias.Neutral;

        var ordered = factors
            .OrderBy(f => context.Factors.TryGetValue(f.FactorCode, out var fc) ? fc.DisplayOrder : int.MaxValue)
            .ToList();

        return new ScoreResult(score, bias, coverage, coverage >= profile.MinCoverage, ordered);
    }
}

public interface IScoringStrategyResolver
{
    IScoringStrategy Resolve(Market market);
}

public sealed class ScoringStrategyResolver(IEnumerable<IScoringStrategy> strategies) : IScoringStrategyResolver
{
    private readonly IReadOnlyList<IScoringStrategy> _strategies = strategies.ToList();

    public IScoringStrategy Resolve(Market market) =>
        _strategies.FirstOrDefault(s => s.Handles(market))
        ?? throw new InvalidOperationException($"No scoring strategy handles market {market}.");
}
