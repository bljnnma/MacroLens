using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database.Configurations;

public class IndicatorReleaseConfiguration : IEntityTypeConfiguration<IndicatorRelease>
{
    public void Configure(EntityTypeBuilder<IndicatorRelease> b)
    {
        b.ToTable("indicator_releases");
        b.HasKey(x => x.Id);

        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.Actual).HasPrecision(18, 6);
        b.Property(x => x.Forecast).HasPrecision(18, 6);
        b.Property(x => x.Previous).HasPrecision(18, 6);
        b.Property(x => x.SourceRef).HasMaxLength(200);

        b.HasOne(x => x.Indicator)
            .WithMany()
            .HasForeignKey(x => x.IndicatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // The natural key. Double-ingestion is the most common data bug in this
        // class of app, and revision is part of the key so restatements are
        // additive rather than destructive.
        b.HasIndex(x => new { x.IndicatorId, x.CurrencyCode, x.Period, x.Revision }).IsUnique();

        // The engine's hot path: latest release per currency.
        b.HasIndex(x => new { x.CurrencyCode, x.ReleasedAt })
            .IsDescending(false, true);
    }
}

public class SeriesObservationConfiguration : IEntityTypeConfiguration<SeriesObservation>
{
    public void Configure(EntityTypeBuilder<SeriesObservation> b)
    {
        b.ToTable("series_observations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasPrecision(18, 6);

        // One index, not two: Postgres scans a btree backwards, so the unique
        // ascending index already serves the "latest N observations" query.
        b.HasIndex(x => new { x.SeriesId, x.ObservedAt }).IsUnique();
    }
}
