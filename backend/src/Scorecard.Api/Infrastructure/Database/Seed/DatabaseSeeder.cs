using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database.Seed;

/// <summary>
/// Reference data is configuration and ships with the app. Transactional data
/// (releases, observations) is seeded here only because the MVP has no provider
/// integration yet — every row is stamped Source = Manual so it stays
/// distinguishable once real feeds land.
/// </summary>
public sealed class DatabaseSeeder(AppDbContext db, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedReferenceAsync(ct);

        // Everything below runs on every startup, unlike the block above. Seed
        // reference data is CONFIGURATION: changing a staleness window or a
        // display name must take effect without a database reset.
        //
        // This was learned the hard way. C3b raised CPI's MaxAgeDays from 75 to
        // 130 for New Zealand's quarterly reporting, the seeder skipped because
        // the database was already populated, and the change silently never
        // landed — the symptom looked like a threshold that was too tight rather
        // than a threshold that was never applied.
        await ReconcileReferenceMetadataAsync(ct);
        await ReconcileCurrencyPoliciesAsync(ct);
        await ReconcileProfilesAsync(ct);
        await ReconcileIndicatorSourcesAsync(ct);
        await ReconcileSchedulesAsync(ct);
    }

    /// <summary>Central bank mandates — configuration, upserted by currency.</summary>
    private async Task ReconcileCurrencyPoliciesAsync(CancellationToken ct)
    {
        var existing = await db.CurrencyPolicies.ToDictionaryAsync(p => p.CurrencyCode, ct);
        var changed = 0;

        foreach (var seed in SeedData.CurrencyPolicies())
        {
            if (existing.TryGetValue(seed.CurrencyCode, out var row))
            {
                if (row.InflationTarget == seed.InflationTarget
                    && row.ToleranceBand == seed.ToleranceBand
                    && row.Authority == seed.Authority)
                    continue;

                row.InflationTarget = seed.InflationTarget;
                row.ToleranceBand = seed.ToleranceBand;
                row.Authority = seed.Authority;
                changed++;
                continue;
            }

            db.CurrencyPolicies.Add(seed);
            changed++;
        }

        if (changed == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Currency policies reconciled: {Changed} row(s).", changed);
    }

    /// <summary>
    /// Ensures the seed's profile version exists and is the only active one.
    ///
    /// Rule R3 says a profile is immutable once used, so a weight change is never
    /// an UPDATE: it is a new version, inserted alongside its predecessor, which
    /// is then deactivated. Every score already written keeps pointing at the
    /// profile that produced it and stays reproducible.
    ///
    /// Bumping SeedData.ProfileVersion is therefore the entire ceremony for
    /// shipping a new weighting — no migration, no manual SQL.
    /// </summary>
    private async Task ReconcileProfilesAsync(CancellationToken ct)
    {
        var target = SeedData.ProfileVersion;

        var existing = await db.ScoringProfiles
            .Include(p => p.Weights)
            .ToListAsync(ct);

        var seeded = SeedData.Profiles(DateTimeOffset.UtcNow);
        var added = 0;
        var deactivated = 0;

        foreach (var profile in seeded)
        {
            var forMarket = existing.Where(p => p.Market == profile.Market).ToList();

            if (forMarket.Any(p => p.Version == target))
                continue;

            // Older versions lose active status first: a partial unique index
            // enforces one active profile per market, and inserting before
            // clearing would violate it inside the same transaction.
            foreach (var old in forMarket.Where(p => p.IsActive))
            {
                old.IsActive = false;
                deactivated++;
            }

            db.ScoringProfiles.Add(profile);
            added++;
        }

        if (added == 0 && deactivated == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Scoring profiles reconciled to v{Version}: {Added} added, {Deactivated} deactivated.",
            target, added, deactivated);
    }

    /// <summary>
    /// Brings mutable reference metadata in line with the seed.
    ///
    /// Safe against rule R4: scores snapshot their own inputs and store their own
    /// explanations, so changing a band or a label here affects the NEXT
    /// calculation and never rewrites a past one. Identity — codes and ids — is
    /// never touched.
    /// </summary>
    private async Task ReconcileReferenceMetadataAsync(CancellationToken ct)
    {
        var changed = 0;

        var factors = await db.Factors.ToDictionaryAsync(f => f.Code, ct);
        foreach (var seed in SeedData.Factors())
        {
            // A factor added to the seed must appear, not be skipped — profile v2
            // introduces LABOUR, and a weight row pointing at a factor that does
            // not exist would simply never score.
            if (!factors.TryGetValue(seed.Code, out var row))
            {
                db.Factors.Add(seed);
                changed++;
                continue;
            }

            if (row.Name == seed.Name && row.ShortName == seed.ShortName
                && row.Description == seed.Description
                && row.Category == seed.Category && row.Scope == seed.Scope
                && row.DisplayOrder == seed.DisplayOrder)
                continue;

            row.Name = seed.Name;
            row.ShortName = seed.ShortName;
            row.Description = seed.Description;
            row.Category = seed.Category;
            row.Scope = seed.Scope;
            row.DisplayOrder = seed.DisplayOrder;
            changed++;
        }

        var indicators = await db.Indicators.ToDictionaryAsync(i => i.Code, ct);
        foreach (var seed in SeedData.Indicators())
        {
            if (!indicators.TryGetValue(seed.Code, out var row))
            {
                db.Indicators.Add(seed);
                changed++;
                continue;
            }

            if (row.FactorCode == seed.FactorCode
                && row.MaxAgeDays == seed.MaxAgeDays && row.CurrencyDirection == seed.CurrencyDirection
                && row.Unit == seed.Unit && row.Impact == seed.Impact
                && row.BandMinor == seed.BandMinor && row.BandMajor == seed.BandMajor
                && row.IsProxy == seed.IsProxy
                && row.Name == seed.Name && row.Description == seed.Description)
                continue;

            // Which factor an indicator feeds is configuration, and profile v2
            // moved the labour indicators from NFP to LABOUR.
            row.FactorCode = seed.FactorCode;
            row.MaxAgeDays = seed.MaxAgeDays;
            row.CurrencyDirection = seed.CurrencyDirection;
            row.Unit = seed.Unit;
            row.Impact = seed.Impact;
            row.BandMinor = seed.BandMinor;
            row.BandMajor = seed.BandMajor;
            row.IsProxy = seed.IsProxy;
            row.Name = seed.Name;
            row.Description = seed.Description;
            row.WhyItMatters = seed.WhyItMatters;
            row.HowItAffects = seed.HowItAffects;
            changed++;
        }

        var series = await db.MarketSeries.ToDictionaryAsync(s => s.Code, ct);
        foreach (var seed in SeedData.Series())
        {
            if (!series.TryGetValue(seed.Code, out var row)) continue;

            if (row.MaxAgeDays == seed.MaxAgeDays && row.Cadence == seed.Cadence
                && row.Frequency == seed.Frequency && row.Unit == seed.Unit
                && row.ProviderSeriesId == seed.ProviderSeriesId
                && row.Name == seed.Name && row.Description == seed.Description
                && row.ScaleNote == seed.ScaleNote)
                continue;

            row.MaxAgeDays = seed.MaxAgeDays;
            row.Cadence = seed.Cadence;
            row.Frequency = seed.Frequency;
            row.Unit = seed.Unit;
            row.ProviderSeriesId = seed.ProviderSeriesId;
            row.Name = seed.Name;
            row.Description = seed.Description;
            row.ScaleNote = seed.ScaleNote;
            changed++;
        }

        if (changed == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Reference metadata reconciled: {Changed} row(s) updated.", changed);
    }

    private async Task SeedReferenceAsync(CancellationToken ct)
    {
        if (await db.Factors.AnyAsync(ct))
        {
            logger.LogInformation("Seed skipped: reference data already present.");
            return;
        }

        var asOf = DateTimeOffset.UtcNow;
        var bundle = SeedData.Build(asOf);

        db.Factors.AddRange(bundle.Factors);
        db.CurrencyPolicies.AddRange(SeedData.CurrencyPolicies());
        db.Indicators.AddRange(bundle.Indicators);
        db.Assets.AddRange(bundle.Assets);
        db.MarketSeries.AddRange(bundle.Series);
        db.ScoringProfiles.AddRange(bundle.Profiles);
        db.IndicatorReleases.AddRange(bundle.Releases);
        db.SeriesObservations.AddRange(bundle.Observations);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded {Factors} factors, {Assets} assets, {Releases} releases, {Observations} observations.",
            bundle.Factors.Count, bundle.Assets.Count, bundle.Releases.Count, bundle.Observations.Count);
    }

    /// <summary>
    /// One row per (indicator, currency) the seed knows a provider for. Mappings
    /// are configuration, so a changed provider or series id has to reach rows
    /// that already exist; only the mapping is overwritten, never the data it
    /// has already ingested.
    /// </summary>
    private async Task ReconcileIndicatorSourcesAsync(CancellationToken ct)
    {
        var indicatorIds = await db.Indicators.ToDictionaryAsync(i => i.Code, i => i.Id, ct);
        var existing = await db.IndicatorSources
            .ToDictionaryAsync(s => (s.IndicatorId, s.CurrencyCode), ct);

        var added = 0;
        var updated = 0;

        foreach (var seed in SeedData.AllReleaseSources())
        {
            if (!indicatorIds.TryGetValue(seed.IndicatorCode, out var indicatorId))
            {
                logger.LogWarning(
                    "Provider mapping references unknown indicator {Code}; skipped.", seed.IndicatorCode);
                continue;
            }

            if (existing.TryGetValue((indicatorId, seed.Currency), out var row))
            {
                if (row.Provider == seed.Provider
                    && row.ProviderSeriesId == seed.ProviderSeriesId
                    && row.Transform == seed.Transform
                    && row.Cadence == seed.Cadence)
                    continue;

                row.Provider = seed.Provider;
                row.ProviderSeriesId = seed.ProviderSeriesId;
                row.Transform = seed.Transform;
                row.Cadence = seed.Cadence;
                updated++;
                continue;
            }

            db.IndicatorSources.Add(new IndicatorSource
            {
                Id = Guid.NewGuid(),
                IndicatorId = indicatorId,
                CurrencyCode = seed.Currency,
                Provider = seed.Provider,
                ProviderSeriesId = seed.ProviderSeriesId,
                Transform = seed.Transform,
                Cadence = seed.Cadence,
                IsEnabled = true
            });
            added++;
        }

        // A mapping removed from the seed must stop polling. Disabled rather than
        // deleted: the rows it already ingested stay explainable, and the reason
        // a source was retired is worth keeping.
        var wanted = SeedData.AllReleaseSources()
            .Where(s => indicatorIds.ContainsKey(s.IndicatorCode))
            .Select(s => (indicatorIds[s.IndicatorCode], s.Currency))
            .ToHashSet();

        var retired = existing.Values
            .Where(row => row.IsEnabled && !wanted.Contains((row.IndicatorId, row.CurrencyCode)))
            .ToList();

        foreach (var row in retired) row.IsEnabled = false;

        if (added == 0 && updated == 0 && retired.Count == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Indicator sources reconciled: {Added} added, {Updated} updated, {Retired} retired.",
            added, updated, retired.Count);
    }

    /// <summary>
    /// One schedule row per provider-backed source. Indicator sources are keyed
    /// by currency as well as code — after C3b the euro area and New Zealand
    /// feed the same indicator on different rhythms.
    /// </summary>
    private async Task ReconcileSchedulesAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var seriesSources = await db.MarketSeries
            .Where(s => s.ProviderSeriesId != null)
            .Select(s => new { s.Code, s.Cadence })
            .ToListAsync(ct);

        var indicatorSources = await db.IndicatorSources
            .Where(s => s.IsEnabled)
            .Select(s => new { s.Indicator!.Code, s.CurrencyCode, s.Cadence })
            .ToListAsync(ct);

        var wanted = seriesSources
            .Select(s => (Kind: SyncSourceKind.Series, s.Code, Currency: string.Empty, s.Cadence))
            .Concat(indicatorSources.Select(s =>
                (Kind: SyncSourceKind.Indicator, s.Code, Currency: s.CurrencyCode, s.Cadence)))
            .ToList();

        var existing = await db.SyncSchedules
            .ToDictionaryAsync(s => (s.SourceKind, s.SourceCode, s.SourceCurrency), ct);

        var added = 0;
        var retuned = 0;

        foreach (var (kind, code, currency, cadence) in wanted)
        {
            if (existing.TryGetValue((kind, code, currency), out var row))
            {
                if (row.Cadence == cadence) continue;

                // Cadence changed in config. NextDueAt is left alone: retuning
                // how patient we are should not trigger an unscheduled poll.
                row.Cadence = cadence;
                retuned++;
                continue;
            }

            db.SyncSchedules.Add(new SyncSchedule
            {
                Id = Guid.NewGuid(),
                SourceKind = kind,
                SourceCode = code,
                SourceCurrency = currency,
                Cadence = cadence,
                IsEnabled = true,
                // Due immediately, so a fresh install pulls real data on its
                // first tick rather than waiting out an interval.
                NextDueAt = now
            });
            added++;
        }

        // A schedule whose mapping was removed must stop polling, or it fails
        // forever against a source that no longer exists.
        var orphans = existing
            .Where(kv => !wanted.Any(w => (w.Kind, w.Code, w.Currency) == kv.Key))
            .Select(kv => kv.Value)
            .ToList();

        if (orphans.Count > 0) db.SyncSchedules.RemoveRange(orphans);

        if (added == 0 && retuned == 0 && orphans.Count == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Sync schedules reconciled: {Added} added, {Retuned} re-tuned, {Removed} removed.",
            added, retuned, orphans.Count);
    }
}
