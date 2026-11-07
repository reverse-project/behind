using Behind.Common.Modules;
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
        (options, connectionString) => options.UseVersioningSqlite(connectionString),
        (options, connectionString, historyTable) => options.UseSqlite(
            connectionString,
            sqlite => sqlite.MigrationsHistoryTable(historyTable)));

    extension(DbContextOptionsBuilder options)
    {
        public DbContextOptionsBuilder UseVersioningSqlite(string? connectionString) =>
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsHistoryTable(VersioningContextSqlite.MigrationsHistoryTable));
    }

    extension(DatabaseConfiguration database)
    {
        public WebApplicationBuilder RegisterSqlite(WebApplicationBuilder builder) =>
            database.RegisterSqlite<CoreContextSqlite>(builder);

        public WebApplicationBuilder RegisterSqlite<TContext>(WebApplicationBuilder builder)
            where TContext : AbstractCoreContext
        {
            database.RegisterProvider<SqliteProvider>(
                builder.Services,
                builder.Configuration,
                services => services
                    .AddAuditedDatabase<TContext, VersioningContextSqlite, SqliteCoreDatabaseMigrator>(
                        builder.Configuration,
                        SqliteProviderOptions)
                    .AddCoreMigrationDatabase<CoreContextSqlite>(builder.Configuration, SqliteProviderOptions));

            return builder;
        }

        public HostApplicationBuilder RegisterSqlite(HostApplicationBuilder builder) =>
            database.RegisterSqlite<CoreContextSqlite>(builder);

        public HostApplicationBuilder RegisterSqlite<TContext>(HostApplicationBuilder builder)
            where TContext : AbstractCoreContext
        {
            database.RegisterProvider<SqliteProvider>(
                builder.Services,
                builder.Configuration,
                services => services
                    .AddMigrationDatabase<TContext, VersioningContextSqlite, SqliteCoreDatabaseMigrator>(
                        builder.Configuration,
                        SqliteProviderOptions)
                    .AddCoreMigrationDatabase<CoreContextSqlite>(builder.Configuration, SqliteProviderOptions));

            return builder;
        }

        public HostApplicationBuilder RegisterSqliteModule<TContext>(
            HostApplicationBuilder builder,
            ModuleDescriptor module)
            where TContext : DbContext
        {
            database.RegisterProvider<SqliteProvider>(
                builder.Services,
                builder.Configuration,
                services => services.AddModuleMigrationDatabase<TContext>(
                    module,
                    builder.Configuration,
                    SqliteProviderOptions));

            return builder;
        }
    }
}
