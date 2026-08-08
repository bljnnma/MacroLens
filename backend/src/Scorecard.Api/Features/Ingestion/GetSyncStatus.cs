using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Ingestion;

public sealed record SyncScheduleView(
    SyncSourceKind SourceKind,
    string SourceCode,
    string SourceCurrency,
    SyncCadence Cadence,
    bool IsEnabled,
    DateTimeOffset NextDueAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastChangeAt,
    int ConsecutiveFailures,
    bool IsOverdue,
    int CheckIntervalHours,
    int OverdueAfterDays,
    string? LastError);

public sealed record SyncStatusResponse(
    DateTimeOffset AsOf,
    bool ProviderConfigured,
    int OverdueCount,
    int FailingCount,
    IReadOnlyList<SyncScheduleView> Schedules);

/// <summary>
/// The scheduler's only window. Without it a background job is a black box: you
/// cannot tell a quiet feed from a broken one, which is the failure mode that
/// silently degrades coverage.
/// </summary>
public sealed class GetSyncStatusHandler(AppDbContext db)
{
    public async Task<SyncStatusResponse> HandleAsync(bool providerConfigured, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var schedules = await db.SyncSchedules
            .AsNoTracking()
            .OrderBy(s => s.SourceKind)
            .ThenBy(s => s.SourceCode)
            .ThenBy(s => s.SourceCurrency)
            .ToListAsync(ct);

        var views = schedules
            .Select(s => new SyncScheduleView(
                s.SourceKind,
                s.SourceCode,
                s.SourceCurrency,
                s.Cadence,
                s.IsEnabled,
                s.NextDueAt,
                s.LastAttemptAt,
                s.LastSuccessAt,
                s.LastChangeAt,
                s.ConsecutiveFailures,
                SyncCadencePolicy.IsOverdue(s, now),
                (int)SyncCadencePolicy.CheckInterval(s.Cadence).TotalHours,
                (int)SyncCadencePolicy.OverdueAfter(s.Cadence).TotalDays,
                s.LastError))
            .ToList();

        return new SyncStatusResponse(
            now,
            providerConfigured,
            views.Count(v => v.IsOverdue),
            views.Count(v => v.ConsecutiveFailures > 0),
            views);
    }
}

public sealed class GetSyncStatusEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/sync/status", async (
                GetSyncStatusHandler handler,
                Infrastructure.Providers.FredClient fred,
                CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(fred.IsConfigured, ct)))
            .WithName("GetSyncStatus")
            .WithTags("Admin")
            .Produces<SyncStatusResponse>();

        // Same code path the worker runs, so a manual run proves the scheduled
        // one. `force` ignores NextDueAt for the times you need an answer now.
        app.MapPost("/api/v1/admin/sync/run", async (
                SyncRunner runner, bool? force, CancellationToken ct) =>
            Results.Ok(await runner.RunAsync(DateTimeOffset.UtcNow, force ?? false, ct)))
            .WithName("RunSyncTick")
            .WithTags("Admin")
            .Produces<SyncTickResult>();
    }
}
