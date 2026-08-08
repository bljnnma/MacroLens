using System.Diagnostics;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Scoring;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Scores;

public sealed record CalculateScoresResponse(
    int AssetsScored, DateTimeOffset DataAsOf, DateTimeOffset CalculatedAt, string EngineVersion);

public sealed class CalculateScoresHandler(
    AppDbContext db,
    ScoringDataLoader loader,
    IScoringStrategyResolver resolver)
{
    public async Task<CalculateScoresResponse> HandleAsync(CancellationToken ct)
    {
        var asOf = DateTimeOffset.UtcNow;
        var data = await loader.LoadAsync(asOf, ct);
        var calculatedAt = DateTimeOffset.UtcNow;
        var created = new List<AssetScore>();

        foreach (var asset in data.Assets)
        {
            if (!data.Profiles.TryGetValue(asset.Market, out var profile)) continue;

            var stopwatch = Stopwatch.StartNew();
            var context = ScoringDataLoader.BuildContext(data, asset, profile, asOf);
            var result = resolver.Resolve(asset.Market).Score(context);
            stopwatch.Stop();

            // A new row every run — never an update. Rule R2: history is not
            // rewritten, which is what keeps old scores reproducible.
            var score = new AssetScore
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                Score = result.Score,
                Bias = result.Bias,
                Coverage = result.Coverage,
                IsSufficient = result.IsSufficient,
                ScoringProfileId = profile.Id,
                ProfileVersion = profile.Version,
                EngineVersion = EngineVersion.Current,
                DataAsOf = data.DataAsOf,
                CalculatedAt = calculatedAt,
                CalculationDurationMs = (int)stopwatch.ElapsedMilliseconds
            };

            foreach (var factor in result.Factors)
            {
                score.Factors.Add(new AssetFactorScore
                {
                    Id = Guid.NewGuid(),
                    AssetScoreId = score.Id,
                    FactorCode = factor.FactorCode,
                    RawValue = factor.RawValue,
                    RawLabelMn = factor.RawLabelMn,
                    RawLabelEn = factor.RawLabelEn,
                    NormalizedScore = factor.Normalized,
                    Weight = factor.Weight,
                    Polarity = factor.Polarity,
                    Contribution = factor.Contribution,
                    ExplanationMn = factor.ExplanationMn,
                    ExplanationEn = factor.ExplanationEn,
                    Readings = factor.Readings.ToList()
                });
            }

            created.Add(score);
        }

        db.AssetScores.AddRange(created);
        await db.SaveChangesAsync(ct);

        return new CalculateScoresResponse(created.Count, data.DataAsOf, calculatedAt, EngineVersion.Current);
    }
}

public sealed class CalculateScoresEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/admin/scores/calculate", async (
                CalculateScoresHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("CalculateScores")
            .WithTags("Admin");
}
