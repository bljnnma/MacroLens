using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Features.Scores;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Providers;

namespace Scorecard.Api.Features.Ingestion;

public sealed record SyncSourceOutcome(
    SyncSourceKind SourceKind,
    string SourceCode,
    string SourceCurrency,
    SyncCadence Cadence,
    bool Succeeded,
    int RowsChanged,
    DateTimeOffset NextDueAt,
    string? Error);

public sealed record SyncTickResult(
    DateTimeOffset RanAt,
    bool ProviderConfigured,
    IReadOnlyList<SyncSourceOutcome> Sources,
    bool Recalculated,
    int AssetsScored);

/// <summary>
/// One pass of the scheduler: claim what is due, poll each source on its own
/// cadence, and recompute scores once if anything actually landed.
///
/// Kept out of the BackgroundService so the same code path serves the manual
/// POST /admin/sync/run endpoint — the worker is a timer, not a behaviour.
/// </summary>
public sealed class SyncRunner(
    AppDbContext db,
    SyncSeriesHandler seriesHandler,
    SyncReleasesHandler releasesHandler,
    CalculateScoresHandler scoresHandler,
    FredClient fred,
    ILogger<SyncRunner> logger)
{
    /// <summary>
    /// Arbitrary but stable key for the Postgres advisory lock that serialises
    /// ticks. Cluster-wide, so it holds across processes as well as within one.
    /// </summary>
    private const long TickLockKey = 8_675_309_001L;

    /// <summary>
    /// Runs one tick under an advisory lock, or returns immediately if another
    /// tick already holds it.
    ///
    /// Claiming due rows is not enough on its own, which a real collision proved:
    /// the background worker and a manual force run overlapped, both fetched
    /// every series, and the second one hit the unique index on
    /// indicator_releases after the first had already inserted. Claiming stops a
    /// *later* tick from re-picking the same source; it does nothing about a tick
    /// already in flight, and force skips the due check entirely.
    /// </summary>
    public async Task<SyncTickResult> RunAsync(DateTimeOffset now, bool force, CancellationToken ct)
    {
        // Session-level rather than transaction-level: a tick makes minutes of
        // HTTP calls, and holding a transaction open across them would pin a
        // connection and block vacuum for the duration.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await TryAcquireTickLockAsync(ct))
            {
                logger.LogInformation("Sync tick skipped: another tick is already running.");
                return new SyncTickResult(now, true, [], false, 0);
            }

            try
            {
                return await RunLockedAsync(now, force, ct);
            }
            finally
            {
                await ReleaseTickLockAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> TryAcquireTickLockAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT pg_try_advisory_lock({TickLockKey});";
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private async Task ReleaseTickLockAsync()
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT pg_advisory_unlock({TickLockKey});";
        // No cancellation token: the lock must be released even when the tick was
        // cancelled, or it survives until the connection is recycled.
        await command.ExecuteScalarAsync(CancellationToken.None);
    }

    private async Task<SyncTickResult> RunLockedAsync(DateTimeOffset now, bool force, CancellationToken ct)
    {
        // FRED is the only provider needing a key, and it backs every market
        // series plus all of USD. Without it there is nothing worth a tick —
        // BIS, Eurostat and the OECD are open, but a partial run would push
        // their due times forward while USD silently rots.
        if (!fred.IsConfigured)
        {
            // Nothing is claimed and no due time moves forward, so configuring
            // the key later makes every source immediately due rather than
            // leaving the schedule silently pushed into the future.
            logger.LogWarning("Sync tick skipped: Fred:ApiKey is not configured.");
            return new SyncTickResult(now, false, [], false, 0);
        }

        var due = await db.SyncSchedules
            .Where(s => s.IsEnabled && (force || s.NextDueAt <= now))
            .OrderBy(s => s.NextDueAt)
            .ToListAsync(ct);

        if (due.Count == 0)
            return new SyncTickResult(now, true, [], false, 0);

        // Claim before the first network call, so a process that dies mid-tick
        // leaves its sources waiting one interval rather than being retried on
        // every restart. Overlap between two live ticks is handled by the
        // advisory lock in RunAsync, not here.
        foreach (var schedule in due)
        {
            schedule.LastAttemptAt = now;
            schedule.NextDueAt = now + SyncCadencePolicy.CheckInterval(schedule.Cadence);
        }
        await db.SaveChangesAsync(ct);

        var outcomes = new List<SyncSourceOutcome>(due.Count);
        var changed = false;

        foreach (var schedule in due)
        {
            var outcome = await PollAsync(schedule, now, ct);
            outcomes.Add(outcome);
            if (outcome.RowsChanged > 0) changed = true;
        }

        await db.SaveChangesAsync(ct);

        // At most one recalculation per tick. Scoring reads the whole universe
        // in a fixed number of queries, so running it once after five sources
        // landed costs the same as running it after one — and running it five
        // times would write five near-identical score rows.
        var recalculated = false;
        var assetsScored = 0;

        if (changed)
        {
            var result = await scoresHandler.HandleAsync(ct);
            recalculated = true;
            assetsScored = result.AssetsScored;

            logger.LogInformation(
                "Sync tick wrote {Sources} source(s); rescored {Assets} assets.",
                outcomes.Count(o => o.RowsChanged > 0), assetsScored);
        }

        return new SyncTickResult(now, true, outcomes, recalculated, assetsScored);
    }

    private async Task<SyncSourceOutcome> PollAsync(
        SyncSchedule schedule, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var changed = schedule.SourceKind switch
            {
                SyncSourceKind.Series => await PollSeriesAsync(schedule.SourceCode, ct),
                SyncSourceKind.Indicator =>
                    await PollIndicatorAsync(schedule.SourceCode, schedule.SourceCurrency, ct),
                _ => throw new InvalidOperationException($"Unknown source kind {schedule.SourceKind}.")
            };

            schedule.LastSuccessAt = now;
            schedule.ConsecutiveFailures = 0;
            schedule.LastError = null;

            if (changed > 0) schedule.LastChangeAt = now;

            // Re-derived from a zeroed failure count so a recovered source drops
            // straight back to its normal rhythm rather than serving out the
            // remainder of a long backoff.
            schedule.NextDueAt = SyncCadencePolicy.NextDue(schedule.Cadence, now, 0);

            return new SyncSourceOutcome(
                schedule.SourceKind, schedule.SourceCode, schedule.SourceCurrency, schedule.Cadence,
                true, changed, schedule.NextDueAt, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            schedule.ConsecutiveFailures++;
            schedule.LastError = Truncate(ex.Message, 500);
            schedule.NextDueAt = SyncCadencePolicy.NextDue(
                schedule.Cadence, now, schedule.ConsecutiveFailures);

            logger.LogError(ex,
                "Sync failed for {Kind} {Code} {Currency} (attempt {Attempt}); next due {NextDue:u}.",
                schedule.SourceKind, schedule.SourceCode, schedule.SourceCurrency,
                schedule.ConsecutiveFailures, schedule.NextDueAt);

            return new SyncSourceOutcome(
                schedule.SourceKind, schedule.SourceCode, schedule.SourceCurrency, schedule.Cadence,
                false, 0, schedule.NextDueAt, schedule.LastError);
        }
    }

    private async Task<int> PollSeriesAsync(string code, CancellationToken ct)
    {
        var response = await seriesHandler.HandleAsync(code, ct);
        var result = response.Series.FirstOrDefault();

        // The handler skips a series it could not read rather than throwing, so
        // an empty result is the signal that the provider gave us nothing. Left
        // as a failure on purpose: silence from a feed is a fault, not a no-op.
        if (result is null)
            throw new InvalidOperationException($"Provider returned no usable rows for series {code}.");

        return result.Inserted + result.Updated;
    }

    private async Task<int> PollIndicatorAsync(string code, string currency, CancellationToken ct)
    {
        var response = await releasesHandler.HandleAsync(code, currency, ct);
        var result = response.Indicators.FirstOrDefault();

        if (result is null)
            throw new InvalidOperationException(
                $"Provider returned no usable rows for indicator {code}/{currency}.");

        return result.Inserted + result.Updated;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
