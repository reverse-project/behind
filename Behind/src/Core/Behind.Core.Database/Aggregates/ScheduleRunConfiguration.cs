using Behind.Common.Database;
using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ScheduleRunConfiguration : IEntityTypeConfiguration<ScheduleRun>
{
    public void Configure(EntityTypeBuilder<ScheduleRun> builder)
    {
        builder.ToTable("ScheduleRuns");
        builder.HasVersionedKey();

        builder.Property(run => run.ScheduleId).IsRequired();
        builder.Property(run => run.ScheduleName).HasMaxLength(Schedule.MaxNameLength).IsRequired();
        builder.Property(run => run.Action).HasMaxLength(Schedule.MaxActionLength).IsRequired();
        builder.Property(run => run.ScheduledFor).HasConversion<DateTimeOffsetConverter>().IsRequired();
        builder.Property(run => run.StartedAt).HasConversion<DateTimeOffsetConverter>().IsRequired();
        builder.Property(run => run.FinishedAt).HasConversion<DateTimeOffsetConverter>().IsRequired(false);
        builder.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(ScheduleRun.MaxEnumLength)
            .IsRequired();
        builder.Property(run => run.Summary).HasMaxLength(ScheduleRun.MaxSummaryLength).IsRequired();
        builder.Property(run => run.Link).HasMaxLength(ScheduleRun.MaxLinkLength).IsRequired(false);
        builder.Property(run => run.Email)
            .HasConversion<string>()
            .HasMaxLength(ScheduleRun.MaxEnumLength)
            .IsRequired();
        builder.Property(run => run.Recipients).IsRequired();

        builder.HasIndex(run => run.ScheduleId);
        builder.HasIndex(run => run.StartedAt);
        builder.HasIndex(run => run.Status);
    }
}
