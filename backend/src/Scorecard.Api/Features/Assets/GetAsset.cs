using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Database.ReadModel;
using Scorecard.Api.Infrastructure.Localization;
using Scorecard.Api.Scoring;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Assets;

/// <summary>One currency's reading behind a factor, already localized.</summary>
public sealed record FactorReadingDto(
    string Currency,
    short Direction,
    short NormalizedScore,
    decimal? RawValue,
    string Label);

public sealed record FactorContributionDto(
    string FactorCode,
    string FactorName,
    FactorCategory Category,
    decimal? RawValue,
    string RawLabel,
    decimal? NormalizedScore,
    decimal Weight,
    short Polarity,
    decimal Contribution,
    string Explanation,
    bool Available,
    /// <summary>
    /// The per-currency readings the score was built from — two for a pair, one
    /// for a USD-scoped factor. Without them a pair's normalized score cannot be
    /// checked: the cell would show the base currency's value beside a number
    /// derived from both sides.
    /// </summary>
    IReadOnlyList<FactorReadingDto> Readings);

public sealed record AssetDetailResponse(
    string Symbol,
    string Name,
    Market Market,
    decimal BaseScore,
    decimal Score,
    Bias Bias,
    decimal Coverage,
    bool IsSufficient,
    /// <summary>True only when every weighted factor came from a provider.</summary>
    bool IsFullyReal,
    /// <summary>Share of the profile's weight backed by provider data, 0–1.</summary>
    decimal RealShare,
    string ProfileName,
    int ProfileVersion,
    string EngineVersion,
    DateTimeOffset DataAsOf,
    DateTimeOffset CalculatedAt,
    int CalculationDurationMs,
    IReadOnlyList<FactorContributionDto> Factors);

public sealed class GetAssetHandler(
    AppDbContext db,
    LatestScoresQuery query,
    ProvenanceQuery provenance,
    ILocaleContext locale)
{
    public async Task<AssetDetailResponse?> HandleAsync(string symbol, CancellationToken ct)
    {
        var score = await query.LatestForAsync(symbol, ct);
        if (score is null) return null;

        var byAsset = await provenance.EvaluateAsync([score.Asset!], ct);
        var assetProvenance = byAsset.GetValueOrDefault(score.AssetId, AssetProvenance.None);
        var factors = await db.Factors.AsNoTracking().ToDictionaryAsync(f => f.Code, ct);
        var en = locale.Locale == "en";

        var rows = score.Factors
            .OrderBy(f => factors.TryGetValue(f.FactorCode, out var meta) ? meta.DisplayOrder : int.MaxValue)
            .Select(f =>
            {
                factors.TryGetValue(f.FactorCode, out var meta);
                return new FactorContributionDto(
                    f.FactorCode,
                    meta?.Name.For(locale.Locale) ?? f.FactorCode,
                    meta?.Category ?? FactorCategory.Growth,
                    f.RawValue,
                    en ? f.RawLabelEn : f.RawLabelMn,
                    f.NormalizedScore,
                    f.Weight,
                    f.Polarity,
                    f.Contribution,
                    en ? f.ExplanationEn : f.ExplanationMn,
                    f.NormalizedScore is not null,
                    // Base side first, so a pair reads left to right the way its
                    // symbol does.
                    f.Readings
                        .OrderByDescending(r => r.Direction)
                        .Select(r => new FactorReadingDto(
                            r.Currency, r.Direction, r.Normalized, r.RawValue,
                            en ? r.LabelEn : r.LabelMn))
                        .ToList());
            })
            .ToList();

        return new AssetDetailResponse(
            score.Asset!.Symbol,
            score.Asset.Name.For(locale.Locale),
            score.Asset.Market,
            EngineVersion.BaseScore,
            score.Score,
            score.Bias,
            score.Coverage,
            score.IsSufficient,
            assetProvenance.IsFullyReal,
            assetProvenance.RealShare,
            score.ScoringProfile?.Name ?? string.Empty,
            score.ProfileVersion,
            score.EngineVersion,
            score.DataAsOf,
            score.CalculatedAt,
            score.CalculationDurationMs,
            rows);
    }
}

public sealed class GetAssetEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/assets/{symbol}", async (
                string symbol, GetAssetHandler handler, CancellationToken ct) =>
            await handler.HandleAsync(symbol, ct) is { } result
                ? Results.Ok(result)
                : Results.NotFound())
            .WithName("GetAsset")
            .WithTags("Assets")
            .Produces<AssetDetailResponse>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/assets/{symbol}/history", async (
                string symbol, int? days, AppDbContext db, CancellationToken ct) =>
        {
            var since = DateTimeOffset.UtcNow.AddDays(-(days ?? 30));
            var history = await db.AssetScores.AsNoTracking()
                .Where(s => s.Asset!.Symbol == symbol.ToUpperInvariant() && s.CalculatedAt >= since)
                .OrderBy(s => s.CalculatedAt)
                .Select(s => new
                {
                    date = s.CalculatedAt,
                    score = s.Score,
                    bias = s.Bias,
                    coverage = s.Coverage
                })
                .ToListAsync(ct);

            return Results.Ok(history);
        })
            .WithName("GetAssetHistory")
            .WithTags("Assets");
    }
}
