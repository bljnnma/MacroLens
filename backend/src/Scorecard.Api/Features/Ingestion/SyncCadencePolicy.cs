using Scorecard.Api.Domain;

namespace Scorecard.Api.Features.Ingestion;

/// <summary>
/// Turns a publication cadence into the three intervals the scheduler needs.
///
/// The distinction that matters: cadence is how often the provider PUBLISHES,
/// the check interval is how fast we want to NOTICE, and the overdue threshold
/// is how long silence is tolerable before it counts as a fault. They are not
/// the same number and collapsing them is the mistake this class exists to
/// prevent — polling CPI monthly would surface a print two weeks late, while
/// calling a quarterly series stale after a week would fire constantly.
///
/// A pure static, so the whole policy is unit-testable without a clock, a
/// database, or a network.
/// </summary>
public static class SyncCadencePolicy
{
    /// <summary>First retry gap after a failure; doubles up to the check interval.</summary>
    public static readonly TimeSpan BaseBackoff = TimeSpan.FromMinutes(5);

    /// <summary>How often to ask the provider whether anything new has landed.</summary>
    public static TimeSpan CheckInterval(SyncCadence cadence) => cadence switch
    {
        // Business-day series: four looks a day catches the print the same
        // morning without polling a value that cannot move overnight.
        SyncCadence.Daily => TimeSpan.FromHours(6),

        // The provider's own weekly window is wide, so twice a day is enough to
        // land inside it. FRED does not publish an exact release instant.
        SyncCadence.Weekly => TimeSpan.FromHours(12),

        // Deliberately the same as weekly rather than "once a month": the point
        // is to notice the print, and monthly prints arrive on a schedule we do
        // not know precisely enough to sleep through.
        SyncCadence.Monthly => TimeSpan.FromHours(12),

        SyncCadence.Quarterly => TimeSpan.FromHours(24),

        _ => TimeSpan.FromHours(12)
    };

    /// <summary>
    /// How long a source may go without a CHANGE before it is reported as
    /// overdue. Generous by design — a false alarm on every US holiday would
    /// train the reader to ignore the signal.
    /// </summary>
    public static TimeSpan OverdueAfter(SyncCadence cadence) => cadence switch
    {
        // Long weekends plus a holiday.
        SyncCadence.Daily => TimeSpan.FromDays(4),
        SyncCadence.Weekly => TimeSpan.FromDays(10),
        // A monthly print released mid-month can legitimately be six weeks apart
        // when the previous one arrived early.
        SyncCadence.Monthly => TimeSpan.FromDays(45),
        // Quarter end plus the BEA's roughly one-month lag, with headroom.
        SyncCadence.Quarterly => TimeSpan.FromDays(120),
        _ => TimeSpan.FromDays(45)
    };

    /// <summary>
    /// Exponential from <see cref="BaseBackoff"/>, capped at the normal check
    /// interval. Capping rather than growing without bound matters: a source
    /// that has been failing for a day should still be retried on its ordinary
    /// rhythm once the provider recovers.
    /// </summary>
    public static TimeSpan Backoff(SyncCadence cadence, int consecutiveFailures)
    {
        if (consecutiveFailures <= 0) return CheckInterval(cadence);

        // Shift rather than Math.Pow, and clamped before shifting so a long
        // outage cannot overflow the exponent.
        var steps = Math.Min(consecutiveFailures - 1, 10);
        var backoff = BaseBackoff * (1 << steps);
        var ceiling = CheckInterval(cadence);

        return backoff > ceiling ? ceiling : backoff;
    }

    /// <summary>When to look again, given how the last attempt went.</summary>
    public static DateTimeOffset NextDue(SyncCadence cadence, DateTimeOffset now, int consecutiveFailures) =>
        consecutiveFailures == 0
            ? now + CheckInterval(cadence)
            : now + Backoff(cadence, consecutiveFailures);

    /// <summary>
    /// True when the source has gone quiet for longer than its cadence allows.
    /// A source that has never changed is judged from its last successful poll,
    /// so a freshly seeded schedule does not report overdue on day one.
    /// </summary>
    public static bool IsOverdue(SyncSchedule schedule, DateTimeOffset now)
    {
        if (!schedule.IsEnabled) return false;

        var reference = schedule.LastChangeAt ?? schedule.LastSuccessAt;
        if (reference is null) return false;

        return now - reference.Value > OverdueAfter(schedule.Cadence);
    }
}
