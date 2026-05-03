using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(a => a.EntityKey).HasMaxLength(255);
        builder.Property(a => a.EntityName).HasMaxLength(100);
        builder.Property(a => a.Action).HasMaxLength(255);

        builder.HasIndex(e => new { e.EntityName, e.EntityKey });
        builder.HasIndex(e => e.Timestamp);

        builder.Ignore(a => a.User);

        builder.Property<Guid?>("UserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey("UserId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property<Guid?>("BehalfOfId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey("BehalfOfId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(a => a.BehalfOf);
    }
}