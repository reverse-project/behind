using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqliteProvider = Behind.Core.Common.Configuration.Sqlite;

namespace Behind.Core.Database.Sqlite;

public static class CoreSqliteExtensions
{
    private static readonly DatabaseProviderOptions SqliteProviderOptions = new(
        (options, connectionString) => options.UseSqlite(connectionString),
        (options, connectionString) => options.UseVersioningSqlite(connectionString));

    extension(DbContextOptionsBuilder options)
    {
        public DbContextOptionsBuilder UseVersioningSqlite(string? connectionString) =>
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsHistoryTable(VersioningContextSqlite.MigrationsHistoryTable));
    }

    extension(DatabaseConfiguration database)
    {
        public WebApplicationBuilder RegisterSqlite(WebApplicationBuilder builder)
        {
            database.RegisterProvider<SqliteProvider>(
                builder.Services,
                builder.Configuration,
                services => services.AddAuditedDatabase<CoreContextSqlite, VersioningContextSqlite, SqliteCoreDatabaseMigrator>(
                    builder.Configuration,
                    SqliteProviderOptions));

            return builder;
        }

        public HostApplicationBuilder RegisterSqlite(HostApplicationBuilder builder)
        {
            database.RegisterProvider<SqliteProvider>(
                builder.Services,
                builder.Configuration,
                services => services.AddMigrationDatabase<CoreContextSqlite, VersioningContextSqlite, SqliteCoreDatabaseMigrator>(
                    builder.Configuration,
                    SqliteProviderOptions));

            return builder;
        }
    }
}
