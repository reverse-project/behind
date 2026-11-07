using Behind.Common.Modules;
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
        (options, connectionString) => options.UseVersioningPostgres(connectionString),
        (options, connectionString, historyTable) => options.UseNpgsql(
            connectionString,
            postgres => postgres.MigrationsHistoryTable(historyTable)));

    extension(DbContextOptionsBuilder options)
    {
        public DbContextOptionsBuilder UseVersioningPostgres(string connectionString) =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(VersioningContextPostgres.MigrationsHistoryTable));
    }

    extension(DatabaseConfiguration database)
    {
        public WebApplicationBuilder RegisterPostgres(WebApplicationBuilder builder) =>
            database.RegisterPostgres<CoreContextPostgres>(builder);

        public WebApplicationBuilder RegisterPostgres<TContext>(WebApplicationBuilder builder)
            where TContext : AbstractCoreContext
        {
            database.RegisterProvider<PostgresProvider>(
                builder.Services,
                builder.Configuration,
                services => services
                    .AddAuditedDatabase<TContext, VersioningContextPostgres, PostgresCoreDatabaseMigrator>(
                        builder.Configuration,
                        PostgresProviderOptions)
                    .AddCoreMigrationDatabase<CoreContextPostgres>(builder.Configuration, PostgresProviderOptions));

            return builder;
        }

        public HostApplicationBuilder RegisterPostgres(HostApplicationBuilder builder) =>
            database.RegisterPostgres<CoreContextPostgres>(builder);

        public HostApplicationBuilder RegisterPostgres<TContext>(HostApplicationBuilder builder)
            where TContext : AbstractCoreContext
        {
            database.RegisterProvider<PostgresProvider>(
                builder.Services,
                builder.Configuration,
                services => services
                    .AddMigrationDatabase<TContext, VersioningContextPostgres, PostgresCoreDatabaseMigrator>(
                        builder.Configuration,
                        PostgresProviderOptions)
                    .AddCoreMigrationDatabase<CoreContextPostgres>(builder.Configuration, PostgresProviderOptions));

            return builder;
        }

        public HostApplicationBuilder RegisterPostgresModule<TContext>(
            HostApplicationBuilder builder,
            ModuleDescriptor module)
            where TContext : DbContext
        {
            database.RegisterProvider<PostgresProvider>(
                builder.Services,
                builder.Configuration,
                services => services.AddModuleMigrationDatabase<TContext>(
                    module,
                    builder.Configuration,
                    PostgresProviderOptions));

            return builder;
        }
    }
}
