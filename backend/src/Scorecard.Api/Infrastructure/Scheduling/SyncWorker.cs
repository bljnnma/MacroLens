using Microsoft.Extensions.Options;
using Scorecard.Api.Features.Ingestion;
using Scorecard.Api.Infrastructure.Providers;

namespace Scorecard.Api.Infrastructure.Scheduling;

public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>Off in tests and in any environment that should not call FRED.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often the worker asks the DATABASE what is due — not how often it
    /// calls the provider. Cheap enough to keep short: one indexed query.
    /// </summary>
    public int TickSeconds { get; set; } = 60;

    /// <summary>Lets migration, seeding and the first requests settle first.</summary>
    public int StartupDelaySeconds { get; set; } = 15;
}

/// <summary>
/// A timer, nothing more. All behaviour lives in <see cref="SyncRunner"/> so the
/// manual endpoint and the scheduled path cannot drift apart.
/// </summary>
public sealed class SyncWorker(
    IServiceScopeFactory scopes,
    IOptions<SchedulerOptions> options,
    ILogger<SyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Sync scheduler disabled by configuration.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.StartupDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Checked once, not per tick: the key comes from configuration read at
        // startup, so it cannot appear while the process runs. Ticking anyway
        // would log the same warning every minute for the life of the host.
        using (var probe = scopes.CreateScope())
        {
            var fred = probe.ServiceProvider.GetRequiredService<FredClient>();
            if (!fred.IsConfigured)
            {
                logger.LogWarning(
                    "Sync scheduler idle: Fred:ApiKey is not configured. " +
                    "No source will be polled and no due time will move.");
                return;
            }
        }

        logger.LogInformation(
            "Sync scheduler started; checking due sources every {Seconds}s.", settings.TickSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.TickSeconds));

        do
        {
            try
            {
                // A scope per tick: DbContext is scoped, and holding one open for
                // the lifetime of the host would accumulate tracked entities and
                // serve stale reads to every later tick.
                using var scope = scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<SyncRunner>();

                await runner.RunAsync(DateTimeOffset.UtcNow, force: false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The loop must outlive any single failure. A provider outage or
                // a transient database error should cost one tick, not the
                // scheduler for the rest of the process's life.
                logger.LogError(ex, "Sync tick failed; the scheduler will continue.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));

        logger.LogInformation("Sync scheduler stopped.");
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
