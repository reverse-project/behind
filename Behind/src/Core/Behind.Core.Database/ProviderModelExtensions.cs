using Behind.Common.Database.Generators;
using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class ProviderModelExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public ModelBuilder ApplyCoreProviderConventions()
        {
            modelBuilder.ApplyVersioningProviderConventions();
            modelBuilder.Entity<Email>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<User>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<Grant>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<Restriction>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<WallOfShameEntry>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<Role>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<UserAgent>().Property<Guid>("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<ValidSession>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<MagicLink>().Property<Guid>("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<Password>().Property<Guid>("Id").HasValueGenerator<Uuid7ValueGenerator>();

            return modelBuilder;
        }

        public ModelBuilder ApplyVersioningProviderConventions()
        {
            modelBuilder.Entity<AuditLogEntry>().Property("Id").HasValueGenerator<Uuid7ValueGenerator>();
            modelBuilder.Entity<AuditLogEntry>().Property(a => a.Timestamp).HasConversion<DateTimeOffsetConverter>();

            return modelBuilder;
        }
    }
}
