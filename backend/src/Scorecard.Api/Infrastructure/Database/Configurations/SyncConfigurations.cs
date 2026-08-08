using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database.Configurations;

public class SyncScheduleConfiguration : IEntityTypeConfiguration<SyncSchedule>
{
    public void Configure(EntityTypeBuilder<SyncSchedule> b)
    {
        b.ToTable("sync_schedules");
        b.HasKey(x => x.Id);

        b.Property(x => x.SourceCode).HasMaxLength(30).IsRequired();
        // Not fixed-length: market series store "" here, and a bpchar(3) would
        // pad it to three spaces and break equality against string.Empty.
        b.Property(x => x.SourceCurrency).HasMaxLength(3).IsRequired();

        // The natural key. Reconciliation on startup upserts against it, so a
        // duplicate here would mean one source polled twice per tick. Currency
        // is part of it because after C3b one indicator has a schedule per
        // currency.
        b.HasIndex(x => new { x.SourceKind, x.SourceCode, x.SourceCurrency }).IsUnique();

        // The worker's only query: due rows, oldest first. Filtered because
        // disabled rows are never due and should not widen the index.
        b.HasIndex(x => x.NextDueAt).HasFilter("is_enabled");

        b.Property(x => x.LastError).HasMaxLength(500);
    }
}
