using Scorecard.Api.Domain;
using Scorecard.Api.Features.TopSetups;
using Scorecard.Api.Infrastructure.Database.ReadModel;
using Scorecard.Api.Infrastructure.Localization;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Assets;

/// <summary>
/// Every scored asset, ranked, including the ones below the coverage floor.
///
/// Distinct from Top Setups on purpose: that endpoint answers "what should I
/// trade", so it filters to sufficient coverage. This one answers "show me
/// everything", so an insufficient asset must appear WITH its real coverage
/// rather than being hidden or silently reported as complete.
/// </summary>
public sealed class ListAssetsHandler(
    LatestScoresQuery query,
    ProvenanceQuery provenance,
    ILocaleContext locale)
{
    public async Task<IReadOnlyList<TopSetupItem>> HandleAsync(Market? market, CancellationToken ct)
    {
        var scores = await query.LatestAsync(ct);
        var byAsset = await provenance.EvaluateAsync(scores.Select(s => s.Asset!), ct);

        return scores
            .Where(s => market is null || s.Asset!.Market == market)
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

public sealed class ListAssetsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/assets", async (
                ListAssetsHandler handler, Market? market, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(market, ct)))
            .WithName("ListAssets")
            .WithTags("Assets")
            .Produces<IReadOnlyList<TopSetupItem>>();
}
