using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Providers;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Ingestion;

public sealed record SyncSeriesResult(string Code, string ProviderSeriesId, int Fetched, int Inserted, int Updated, int SeedRowsRemoved);

public sealed record SyncSeriesResponse(IReadOnlyList<SyncSeriesResult> Series, DateTimeOffset SyncedAt);

public sealed class SyncSeriesHandler(
    AppDbContext db,
    FredClient fred,
    IOptions<FredOptions> options,
    ILogger<SyncSeriesHandler> logger)
{
    public async Task<SyncSeriesResponse> HandleAsync(string? code, CancellationToken ct)
    {
        var query = db.MarketSeries.Where(s => s.ProviderSeriesId != null);
        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(s => s.Code == code);

        var series = await query.ToListAsync(ct);
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-options.Value.HistoryDays);
        var results = new List<SyncSeriesResult>();

        foreach (var s in series)
        {
            var observations = await fred.GetObservationsAsync(s.ProviderSeriesId!, from, ct);
            if (observations.Count == 0)
            {
                logger.LogWarning("FRED returned no usable rows for {Code}; leaving existing data alone.", s.Code);
                continue;
            }

            // Real data supersedes the seed. Without this the synthetic ramp
            // (DXY 94-110) would sit alongside the Fed's broad index (~120s) in
            // one window and the percentile would be meaningless.
            var seedRows = await db.SeriesObservations
                .Where(o => o.SeriesId == s.Id && o.Source == DataSource.Manual)
                .ExecuteDeleteAsync(ct);

            var existing = await db.SeriesObservations
                .Where(o => o.SeriesId == s.Id)
                .ToDictionaryAsync(o => o.ObservedAt, ct);

            var inserted = 0;
            var updated = 0;

            foreach (var o in observations)
            {
                var at = new DateTimeOffset(o.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

                if (existing.TryGetValue(at, out var row))
                {
                    if (row.Value != o.Value)
                    {
                        row.Value = o.Value;
                        row.Source = DataSource.Fred;
                        updated++;
                    }
                    continue;
                }

                db.SeriesObservations.Add(new SeriesObservation
                {
                    Id = Guid.NewGuid(),
                    SeriesId = s.Id,
                    ObservedAt = at,
                    Value = o.Value,
                    Source = DataSource.Fred
                });
                inserted++;
            }

            s.Source = DataSource.Fred;
            await db.SaveChangesAsync(ct);

            results.Add(new SyncSeriesResult(
                s.Code, s.ProviderSeriesId!, observations.Count, inserted, updated, seedRows));
        }

        return new SyncSeriesResponse(results, DateTimeOffset.UtcNow);
    }
}

public sealed class SyncSeriesEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/admin/series/sync", async (
            SyncSeriesHandler handler, FredClient fred, string? code, CancellationToken ct) =>
        {
            if (!fred.IsConfigured)
                return Results.Problem("Fred:ApiKey is not configured.", statusCode: 503);

            return Results.Ok(await handler.HandleAsync(code, ct));
        })
        .WithName("SyncSeries")
        .WithTags("Admin")
        .Produces<SyncSeriesResponse>();
}
