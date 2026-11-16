using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Behind.Common.FunctionalExtensions;
using PostgresProvider = Behind.Core.Common.Configuration.Postgres;
using SqliteProvider = Behind.Core.Common.Configuration.Sqlite;

namespace Behind.Tests.CoreDatabase;

public sealed class DatabaseProviderRegistrationTests
{
    private const string PostgresConnection = "Host=localhost;Database=behind-test;Username=test;Password=test";

    private static DatabaseConfiguration Configuration(Provider provider) =>
        new(Some(provider), Some(new Key("Test")));

    private static ServiceProvider Register(Provider provider, string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:Test"] = connectionString;
        var database = Configuration(provider);
        database.RegisterSqlite(builder);
        database.RegisterPostgres(builder);

        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task PostgresProvider_RegistersOnlyPostgresContexts()
    {
        await using var services = Register(new PostgresProvider(), PostgresConnection);
        await using var scope = services.CreateAsyncScope();

        Assert.IsType<CoreContextPostgres>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.IsType<VersioningContextPostgres>(scope.ServiceProvider.GetRequiredService<AbstractVersioningContext>());
        Assert.IsType<PostgresCoreDatabaseMigrator>(scope.ServiceProvider.GetRequiredService<ICoreDatabaseMigrator>());
        Assert.Null(scope.ServiceProvider.GetService<CoreContextSqlite>());
    }

    [Fact]
    public async Task SqliteProvider_RegistersOnlySqliteContexts()
    {
        await using var services = Register(new SqliteProvider(), "Data Source=:memory:");
        await using var scope = services.CreateAsyncScope();

        Assert.IsType<CoreContextSqlite>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.IsType<VersioningContextSqlite>(scope.ServiceProvider.GetRequiredService<AbstractVersioningContext>());
        Assert.IsType<SqliteCoreDatabaseMigrator>(scope.ServiceProvider.GetRequiredService<ICoreDatabaseMigrator>());
        Assert.Null(scope.ServiceProvider.GetService<CoreContextPostgres>());
    }

    [Fact]
    public async Task PostgresMigrationHost_RegistersPostgresMigrator()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = PostgresConnection;
        var database = Configuration(new PostgresProvider());
        database.RegisterSqlite(builder);
        database.RegisterPostgres(builder);
        await using var services = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = services.CreateAsyncScope();

        Assert.IsType<CoreContextPostgres>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.IsType<PostgresCoreDatabaseMigrator>(scope.ServiceProvider.GetRequiredService<ICoreDatabaseMigrator>());
    }

    [Fact]
    public async Task PostgresContexts_UseNpgsqlWithSeparateMigrationHistoryTables()
    {
        await using var services = Register(new PostgresProvider(), PostgresConnection);
        await using var scope = services.CreateAsyncScope();
        var core = scope.ServiceProvider.GetRequiredService<AbstractCoreContext>();
        var versioning = scope.ServiceProvider.GetRequiredService<AbstractVersioningContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", core.Database.ProviderName);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", versioning.Database.ProviderName);
        Assert.Contains(
            VersioningContextPostgres.MigrationsHistoryTable,
            versioning.GetService<IHistoryRepository>().GetCreateScript());
        Assert.Contains("__EFMigrationsHistory", core.GetService<IHistoryRepository>().GetCreateScript());
    }

    [Fact]
    public async Task PostgresMigrations_MatchTheModelSnapshots()
    {
        await using var services = Register(new PostgresProvider(), PostgresConnection);
        await using var scope = services.CreateAsyncScope();
        var core = scope.ServiceProvider.GetRequiredService<AbstractCoreContext>();
        var versioning = scope.ServiceProvider.GetRequiredService<AbstractVersioningContext>();

        Assert.False(core.Database.HasPendingModelChanges());
        Assert.False(versioning.Database.HasPendingModelChanges());
        Assert.NotEmpty(core.Database.GetMigrations());
        Assert.NotEmpty(versioning.Database.GetMigrations());
    }

    [Fact]
    public async Task PostgresModel_CoversTheSameEntitiesAsSqlite()
    {
        await using var postgres = Register(new PostgresProvider(), PostgresConnection);
        await using var sqlite = Register(new SqliteProvider(), "Data Source=:memory:");
        await using var postgresScope = postgres.CreateAsyncScope();
        await using var sqliteScope = sqlite.CreateAsyncScope();

        static string[] Entities(IServiceProvider provider) =>
            provider.GetRequiredService<AbstractCoreContext>().Model.GetEntityTypes()
                .Select(entity => entity.Name)
                .Order()
                .ToArray();

        Assert.Equal(Entities(sqliteScope.ServiceProvider), Entities(postgresScope.ServiceProvider));
    }
}
