using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database;

/// <summary>
/// Injected straight into handlers — no repository layer. DbContext is already a
/// unit of work plus a repository, and the only thing another abstraction would
/// buy is swapping Postgres, which will not happen.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Factor> Factors => Set<Factor>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetCurrencyExposure> AssetCurrencyExposures => Set<AssetCurrencyExposure>();
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<IndicatorSource> IndicatorSources => Set<IndicatorSource>();
    public DbSet<CurrencyPolicy> CurrencyPolicies => Set<CurrencyPolicy>();
    public DbSet<MarketSeries> MarketSeries => Set<MarketSeries>();
    public DbSet<IndicatorRelease> IndicatorReleases => Set<IndicatorRelease>();
    public DbSet<SeriesObservation> SeriesObservations => Set<SeriesObservation>();
    public DbSet<ScoringProfile> ScoringProfiles => Set<ScoringProfile>();
    public DbSet<ProfileWeight> ProfileWeights => Set<ProfileWeight>();
    public DbSet<AssetScore> AssetScores => Set<AssetScore>();
    public DbSet<AssetFactorScore> AssetFactorScores => Set<AssetFactorScore>();
    public DbSet<SyncSchedule> SyncSchedules => Set<SyncSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
