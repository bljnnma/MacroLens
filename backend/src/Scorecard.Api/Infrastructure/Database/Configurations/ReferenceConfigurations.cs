using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database.Configurations;

internal static class LocalizedTextBuilderExtensions
{
    public static PropertyBuilder<LocalizedText> AsJsonb(this PropertyBuilder<LocalizedText> b)
    {
        b.HasConversion(LocalizedTextConversion.Converter)
            .Metadata.SetValueComparer(LocalizedTextConversion.Comparer);
        b.HasColumnType("jsonb").IsRequired();
        return b;
    }
}

public class FactorConfiguration : IEntityTypeConfiguration<Factor>
{
    public void Configure(EntityTypeBuilder<Factor> b)
    {
        b.ToTable("factors");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).AsJsonb();
        b.Property(x => x.ShortName).AsJsonb();
        b.Property(x => x.Description).AsJsonb();
    }
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.ToTable("assets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Symbol).IsUnique();
        b.Property(x => x.Name).AsJsonb();

        b.HasMany(x => x.Exposures)
            .WithOne(x => x.Asset!)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AssetCurrencyExposureConfiguration : IEntityTypeConfiguration<AssetCurrencyExposure>
{
    public void Configure(EntityTypeBuilder<AssetCurrencyExposure> b)
    {
        b.ToTable("asset_currency_exposures");
        b.HasKey(x => x.Id);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.HasIndex(x => new { x.AssetId, x.CurrencyCode }).IsUnique();
    }
}

public class IndicatorConfiguration : IEntityTypeConfiguration<Indicator>
{
    public void Configure(EntityTypeBuilder<Indicator> b)
    {
        b.ToTable("indicators");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.FactorCode).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.FactorCode);

        b.Property(x => x.Name).AsJsonb();
        b.Property(x => x.Description).AsJsonb();
        b.Property(x => x.WhyItMatters).AsJsonb();
        b.Property(x => x.HowItAffects).AsJsonb();

        b.Property(x => x.ProviderSeriesId).HasMaxLength(120);

        // numeric, never float: comparing interest rates in floating point bites.
        // Bands are engine v1.0.0 only — v2.0.0 scores level + direction and needs
        // no per-indicator calibration. Retained so v1 scores stay explainable.
        b.Property(x => x.BandMinor).HasPrecision(18, 6);
        b.Property(x => x.BandMajor).HasPrecision(18, 6);

        b.HasMany(x => x.Sources)
            .WithOne(x => x.Indicator!)
            .HasForeignKey(x => x.IndicatorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class IndicatorSourceConfiguration : IEntityTypeConfiguration<IndicatorSource>
{
    public void Configure(EntityTypeBuilder<IndicatorSource> b)
    {
        b.ToTable("indicator_sources");
        b.HasKey(x => x.Id);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.ProviderSeriesId).HasMaxLength(120).IsRequired();

        // One provider per indicator per currency. Two rows would mean the same
        // release ingested twice from different sources, and the natural key on
        // indicator_releases would reject the second — silently, at sync time.
        b.HasIndex(x => new { x.IndicatorId, x.CurrencyCode }).IsUnique();
    }
}

public class CurrencyPolicyConfiguration : IEntityTypeConfiguration<CurrencyPolicy>
{
    public void Configure(EntityTypeBuilder<CurrencyPolicy> b)
    {
        b.ToTable("currency_policies");

        // The currency code IS the key — there is exactly one mandate per
        // currency and no surrogate id would add anything.
        b.HasKey(x => x.CurrencyCode);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength();

        b.Property(x => x.InflationTarget).HasPrecision(4, 2);
        b.Property(x => x.ToleranceBand).HasPrecision(4, 2);
        b.Property(x => x.Authority).AsJsonb();
    }
}

public class MarketSeriesConfiguration : IEntityTypeConfiguration<MarketSeries>
{
    public void Configure(EntityTypeBuilder<MarketSeries> b)
    {
        b.ToTable("market_series");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.FactorCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.ProviderSeriesId).HasMaxLength(40);
        b.Property(x => x.Name).AsJsonb();
        b.Property(x => x.Description).AsJsonb();
        b.Property(x => x.ScaleNote).AsJsonb();

        b.HasMany(x => x.Observations)
            .WithOne(x => x.Series!)
            .HasForeignKey(x => x.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
