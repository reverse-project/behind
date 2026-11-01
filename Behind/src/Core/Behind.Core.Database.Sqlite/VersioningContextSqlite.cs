using Behind.Common.Database.Generators;
using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Sqlite;

public sealed class VersioningContextSqlite(DbContextOptions<VersioningContextSqlite> options)
    : AbstractVersioningContext(options)
{
    public const string MigrationsHistoryTable = "__VersioningMigrationsHistory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLogEntry>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
        modelBuilder.Entity<AuditLogEntry>().Property(a => a.Timestamp).HasConversion<DateTimeOffsetConverter>();
    }
}
