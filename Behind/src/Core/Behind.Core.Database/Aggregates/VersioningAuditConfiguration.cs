using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Aggregates;

public static class VersioningAuditConfiguration
{
    public const string AuditTable = "AuditLogEntries";

    public static void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<AuditLogEntry>(builder =>
        {
            builder.ToTable(AuditTable, table => table.ExcludeFromMigrations());

            builder.Property<Guid>("Id").ValueGeneratedOnAdd();
            builder.HasKey("Id");

            builder.Property(a => a.EntityKey).HasMaxLength(255);
            builder.Property(a => a.EntityName).HasMaxLength(100);
            builder.Property(a => a.Action)
                .HasConversion<AuditLogEntryAuditActionConverter>()
                .HasMaxLength(4000);

            builder.Ignore(a => a.User);
            builder.Ignore(a => a.BehalfOf);
            builder.Property<Guid?>("UserId");
            builder.Property<Guid?>("BehalfOfId");
        });
}
