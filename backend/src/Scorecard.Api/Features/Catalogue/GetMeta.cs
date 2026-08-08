using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Scoring;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Catalogue;

public sealed record MetaResponse(
    string EngineVersion,
    IReadOnlyList<string> ActiveProfiles,
    DateTimeOffset? LastCalculation,
    DateTimeOffset? DataAsOf,
    string DataSource);

/// <summary>
/// What produced the numbers currently on screen. A transparency-first product
/// should be able to answer that without reading the source.
/// </summary>
public sealed class GetMetaEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/meta", async (AppDbContext db, CancellationToken ct) =>
        {
            var profiles = await db.ScoringProfiles.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Market)
                .Select(p => p.Name + " v" + p.Version)
                .ToListAsync(ct);

            var latest = await db.AssetScores.AsNoTracking()
                .OrderByDescending(s => s.CalculatedAt)
                .Select(s => new { s.CalculatedAt, s.DataAsOf })
                .FirstOrDefaultAsync(ct);

            // Provenance is per row in the database; this reports the dominant
            // source so the UI can say plainly where the numbers came from.
            var source = await db.IndicatorReleases.AsNoTracking()
                .GroupBy(r => r.Source)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new MetaResponse(
                EngineVersion.Current,
                profiles,
                latest?.CalculatedAt,
                latest?.DataAsOf,
                source.ToString()));
        })
        .WithName("GetMeta")
        .WithTags("Catalogue")
        .Produces<MetaResponse>();
}
