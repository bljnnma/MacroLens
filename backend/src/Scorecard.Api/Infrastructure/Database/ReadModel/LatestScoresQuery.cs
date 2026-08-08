using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database.ReadModel;

/// <summary>
/// The swap point for materialized views.
///
/// No MV in the MVP — at ~10 assets an indexed query is already sub-millisecond.
/// But every read goes through this class and returns the shape an MV would
/// materialize, so introducing `mv_latest_asset_scores` later changes one method
/// body and no API contract.
/// </summary>
public sealed class LatestScoresQuery(AppDbContext db)
{
    public async Task<List<AssetScore>> LatestAsync(CancellationToken ct = default) =>
        await db.AssetScores
            .AsNoTracking()
            .Include(s => s.Asset)
            .ThenInclude(a => a!.Exposures)
            .Include(s => s.Factors)
            .Where(s => s.CalculatedAt == db.AssetScores
                .Where(x => x.AssetId == s.AssetId)
                .Max(x => x.CalculatedAt))
            .OrderByDescending(s => s.Score)
            .ToListAsync(ct);

    public async Task<AssetScore?> LatestForAsync(string symbol, CancellationToken ct = default) =>
        await db.AssetScores
            .AsNoTracking()
            .Include(s => s.Asset)
            .ThenInclude(a => a!.Exposures)
            .Include(s => s.Factors)
            .Include(s => s.ScoringProfile)
            .Where(s => s.Asset!.Symbol == symbol.ToUpperInvariant())
            .OrderByDescending(s => s.CalculatedAt)
            .FirstOrDefaultAsync(ct);
}
