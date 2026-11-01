using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public abstract class AbstractVersioningContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ActiveVersion> ActiveVersions => Set<ActiveVersion>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ActiveVersionConfiguration());
        VersioningAuditConfiguration.Apply(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Versions must be saved with SaveChangesAsync.");
}
