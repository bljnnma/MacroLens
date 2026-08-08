using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database.ReadModel;
using Scorecard.Api.Infrastructure.Localization;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.TopSetups;

public sealed record TopSetupItem(
    int Rank,
    string Symbol,
    string Name,
    Market Market,
    decimal Score,
    Bias Bias,
    decimal Coverage,
    bool IsSufficient,
    /// <summary>True only when every weighted factor came from a provider.</summary>
    bool IsFullyReal,
    /// <summary>
    /// Share of the profile's weight backed by provider data, 0–1. Reported
    /// alongside the flag because after C3b most pairs sit between the two
    /// extremes — "hybrid" alone would hide the difference between an asset
    /// that is 48% real and one that is 5% real.
    /// </summary>
    decimal RealShare,
    DateTimeOffset DataAsOf);

public sealed class GetTopSetupsHandler(
    LatestScoresQuery query,
    ProvenanceQuery provenance,
    ILocaleContext locale)
{
    public async Task<IReadOnlyList<TopSetupItem>> HandleAsync(
        Market? market, int limit, CancellationToken ct)
    {
        var scores = await query.LatestAsync(ct);
        var byAsset = await provenance.EvaluateAsync(scores.Select(s => s.Asset!), ct);

        return scores
            // Insufficient coverage is excluded from the RANKING but the score
            // still exists and stays visible on the asset's own page.
            .Where(s => s.IsSufficient)
            .Where(s => market is null || s.Asset!.Market == market)
            .Take(limit)
            .Select((s, i) =>
            {
                var p = byAsset.GetValueOrDefault(s.AssetId, AssetProvenance.None);
                return new TopSetupItem(
                    i + 1,
                    s.Asset!.Symbol,
                    s.Asset.Name.For(locale.Locale),
                    s.Asset.Market,
                    s.Score,
                    s.Bias,
                    s.Coverage,
                    s.IsSufficient,
                    p.IsFullyReal,
                    p.RealShare,
                    s.DataAsOf);
            })
            .ToList();
    }
}

public sealed class GetTopSetupsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/top-setups", async (
                GetTopSetupsHandler handler,
                Market? market,
                int? limit,
                CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(market, limit ?? 8, ct)))
            .WithName("GetTopSetups")
            .WithTags("Dashboard")
            // Declared explicitly: without it the OpenAPI document carries no
            // response schema, and the frontend's generated types would be empty.
            .Produces<IReadOnlyList<TopSetupItem>>();
}
