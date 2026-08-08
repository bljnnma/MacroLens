using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database.Configurations;

public class ScoringProfileConfiguration : IEntityTypeConfiguration<ScoringProfile>
{
    public void Configure(EntityTypeBuilder<ScoringProfile> b)
    {
        b.ToTable("scoring_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).AsJsonb();
        b.Property(x => x.BullishThreshold).HasPrecision(5, 2);
        b.Property(x => x.BearishThreshold).HasPrecision(5, 2);
        b.Property(x => x.MinCoverage).HasPrecision(4, 3);

        b.HasIndex(x => new { x.Market, x.Version }).IsUnique();

        // Exactly one active profile per market, enforced by the database rather
        // than by hope. A partial unique index is the only way to say this.
        b.HasIndex(x => x.Market)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_scoring_profiles_active_per_market");

        b.HasMany(x => x.Weights)
            .WithOne(x => x.Profile!)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProfileWeightConfiguration : IEntityTypeConfiguration<ProfileWeight>
{
    public void Configure(EntityTypeBuilder<ProfileWeight> b)
    {
        b.ToTable("profile_weights");
        b.HasKey(x => x.Id);
        b.Property(x => x.FactorCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.Weight).HasPrecision(5, 2);
        b.HasIndex(x => new { x.ProfileId, x.FactorCode }).IsUnique();
    }
}

public class AssetScoreConfiguration : IEntityTypeConfiguration<AssetScore>
{
    public void Configure(EntityTypeBuilder<AssetScore> b)
    {
        b.ToTable("asset_scores");
        b.HasKey(x => x.Id);
        b.Property(x => x.Score).HasPrecision(5, 2);
        b.Property(x => x.Coverage).HasPrecision(4, 3);
        b.Property(x => x.EngineVersion).HasMaxLength(20).IsRequired();

        b.HasOne(x => x.Asset)
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ScoringProfile)
            .WithMany()
            .HasForeignKey(x => x.ScoringProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serves both "latest score for asset" and the history endpoint.
        b.HasIndex(x => new { x.AssetId, x.CalculatedAt }).IsDescending(false, true);

        // Top Setups ranks only sufficient scores, so the filter keeps the index
        // to exactly the rows the dashboard reads.
        b.HasIndex(x => x.CalculatedAt)
            .IsDescending(true)
            .HasFilter("is_sufficient")
            .HasDatabaseName("ix_asset_scores_sufficient_calculated_at");

        b.HasMany(x => x.Factors)
            .WithOne(x => x.AssetScore!)
            .HasForeignKey(x => x.AssetScoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AssetFactorScoreConfiguration : IEntityTypeConfiguration<AssetFactorScore>
{
    public void Configure(EntityTypeBuilder<AssetFactorScore> b)
    {
        b.ToTable("asset_factor_scores");
        b.HasKey(x => x.Id);
        b.Property(x => x.FactorCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.RawValue).HasPrecision(18, 6);

        // numeric(2,1), not smallint: -2.0 to +2.0 in half steps.
        b.Property(x => x.NormalizedScore).HasPrecision(2, 1);

        b.Property(x => x.Weight).HasPrecision(5, 2);
        b.Property(x => x.Contribution).HasPrecision(6, 2);
        b.Property(x => x.RawLabelMn).HasMaxLength(200);
        b.Property(x => x.RawLabelEn).HasMaxLength(200);

        b.Property(x => x.Readings)
            .HasConversion(FactorReadingConversion.Converter)
            .HasColumnType("jsonb")
            .IsRequired()
            .Metadata.SetValueComparer(FactorReadingConversion.Comparer);

        b.HasIndex(x => x.AssetScoreId);
    }
}
