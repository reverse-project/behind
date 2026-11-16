using Behind.Common.Database;
using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("Schedules");
        builder.HasVersionedKey();

        builder.Property(schedule => schedule.Name).HasMaxLength(Schedule.MaxNameLength).IsRequired();
        builder.Property(schedule => schedule.Cron)
            .HasConversion<CronExpressionConverter>()
            .HasMaxLength(Schedule.MaxCronLength)
            .IsRequired();
        builder.Property(schedule => schedule.Action).HasMaxLength(Schedule.MaxActionLength).IsRequired();
        builder.Property(schedule => schedule.Parameters)
            .HasConversion<ScheduleParametersConverter>()
            .HasMaxLength(Schedule.MaxParametersLength)
            .IsRequired();
        builder.Property(schedule => schedule.Enabled).IsRequired();
        builder.Property(schedule => schedule.Notify)
            .HasConversion<string>()
            .HasMaxLength(Schedule.MaxNotifyLength)
            .IsRequired();
        builder.Property(schedule => schedule.Participants)
            .HasConversion<ScheduleParticipantsConverter>()
            .HasMaxLength(Schedule.MaxParticipantsLength)
            .IsRequired();

        builder.HasIndex(schedule => schedule.Name);
    }
}
