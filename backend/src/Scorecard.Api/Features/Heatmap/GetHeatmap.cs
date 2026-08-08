using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Database.ReadModel;
using Scorecard.Api.Infrastructure.Localization;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Heatmap;

public sealed record HeatmapFactor(string Code, string Name, string ShortName, int DisplayOrder);

public sealed record HeatmapCell(
    string FactorCode,
    decimal? NormalizedScore,
    decimal Contribution,
    string RawLabel,
    decimal Weight,
    bool Available,
    /// <summary>
    /// False when this market's profile does not score the factor at all.
    /// "Not modelled here" and "data missing" are different claims and must not
    /// share a glyph in the UI.
    /// </summary>
    bool InProfile,
    /// <summary>
    /// Both sides of the differential. The cell shows one number derived from
    /// two currencies, so the tooltip has to carry both or the value is
    /// unverifiable.
    /// </summary>
    IReadOnlyList<HeatmapReading> Readings);

public sealed record HeatmapReading(string Currency, short Direction, short NormalizedScore, string Label);

public sealed record HeatmapRow(
    string Symbol, string Name, Market Market, decimal Score, Bias Bias, IReadOnlyList<HeatmapCell> Cells);

public sealed record HeatmapResponse(IReadOnlyList<HeatmapFactor> Factors, IReadOnlyList<HeatmapRow> Rows);

public sealed class GetHeatmapHandler(AppDbContext db, LatestScoresQuery query, ILocaleContext locale)
{
    public async Task<HeatmapResponse> HandleAsync(Market? market, int? limit, CancellationToken ct)
    {
        // Columns come from the ACTIVE profiles, not from the whole catalogue.
        // Retired factors keep their catalogue entry so historical scores stay
        // explainable, but a column of "not modelled" glyphs on every row is
        // noise — profile v2 retired four of nine, which would have left the
        // heatmap almost half empty.
        var scoredCodes = await db.ProfileWeights
            .AsNoTracking()
            .Where(w => w.IsEnabled && w.Profile!.IsActive)
            .Select(w => w.FactorCode)
            .Distinct()
            .ToListAsync(ct);

        var scored = scoredCodes.ToHashSet(StringComparer.Ordinal);

        var factors = await db.Factors.AsNoTracking().OrderBy(f => f.DisplayOrder).ToListAsync(ct);
        factors = factors.Where(f => scored.Contains(f.Code)).ToList();

        var scores = await query.LatestAsync(ct);

        var rows = scores
            .Where(s => market is null || s.Asset!.Market == market)
            .Take(limit ?? int.MaxValue)
            .Select(s => new HeatmapRow(
                s.Asset!.Symbol,
                s.Asset.Name.For(locale.Locale),
                s.Asset.Market,
                s.Score,
                s.Bias,
                factors.Select(f =>
                {
                    var cell = s.Factors.FirstOrDefault(x => x.FactorCode == f.Code);
                    return new HeatmapCell(
                        f.Code,
                        cell?.NormalizedScore,
                        cell?.Contribution ?? 0m,
                        cell is null
                            ? "—"
                            : locale.Locale == "en" ? cell.RawLabelEn : cell.RawLabelMn,
                        cell?.Weight ?? 0m,
                        cell?.NormalizedScore is not null,
                        cell is not null,
                        cell is null
                            ? []
                            : cell.Readings
                                .OrderByDescending(r => r.Direction)
                                .Select(r => new HeatmapReading(
                                    r.Currency, r.Direction, r.Normalized,
                                    locale.Locale == "en" ? r.LabelEn : r.LabelMn))
                                .ToList());
                }).ToList()))
            .ToList();

        return new HeatmapResponse(
            factors.Select(f => new HeatmapFactor(
                f.Code, f.Name.For(locale.Locale), f.ShortName.For(locale.Locale), f.DisplayOrder)).ToList(),
            rows);
    }
}

public sealed class GetHeatmapEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/heatmap", async (
                GetHeatmapHandler handler, Market? market, int? limit, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(market, limit, ct)))
            .WithName("GetHeatmap")
            .WithTags("Dashboard")
            .Produces<HeatmapResponse>();
}
