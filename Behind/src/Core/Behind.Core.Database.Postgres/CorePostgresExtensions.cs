using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PostgresProvider = Behind.Core.Common.Configuration.Postgres;

namespace Behind.Core.Database.Postgres;

public static class CorePostgresExtensions
{
    private static readonly DatabaseProviderOptions PostgresProviderOptions = new(
        (options, connectionString) => options.UseNpgsql(connectionString),
        (options, connectionString) => options.UseVersioningPostgres(connectionString));

    extension(DbContextOptionsBuilder options)
    {
        public DbContextOptionsBuilder UseVersioningPostgres(string connectionString) =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(VersioningContextPostgres.MigrationsHistoryTable));
    }

    extension(DatabaseConfiguration database)
    {
        public WebApplicationBuilder RegisterPostgres(WebApplicationBuilder builder)
        {
            database.RegisterProvider<PostgresProvider>(
                builder.Services,
                builder.Configuration,
                services => services.AddAuditedDatabase<CoreContextPostgres, VersioningContextPostgres, PostgresCoreDatabaseMigrator>(
                    builder.Configuration,
                    PostgresProviderOptions));

            return builder;
        }

        public HostApplicationBuilder RegisterPostgres(HostApplicationBuilder builder)
        {
            database.RegisterProvider<PostgresProvider>(
                builder.Services,
                builder.Configuration,
                services => services.AddMigrationDatabase<CoreContextPostgres, VersioningContextPostgres, PostgresCoreDatabaseMigrator>(
                    builder.Configuration,
                    PostgresProviderOptions));

            return builder;
        }
    }
}
