using Behind.Common.Database.Modules;
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
using PostgresProvider = Behind.Core.Common.Configuration.Postgres;
using SqliteProvider = Behind.Core.Common.Configuration.Sqlite;

namespace Behind.Tests.CoreDatabase;

public sealed class ModuleProviderRegistrationTests : IDisposable
{
    private const string PostgresConnection = "Host=localhost;Database=behind-test;Username=test;Password=test";
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"behind-module-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var path in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_file)}*"))
        {
            File.Delete(path);
        }
    }

    private static DatabaseConfiguration Configuration(Provider provider) =>
        new(Some(provider), Some(new Key("Test")));

    private static ServiceProviderOptions Validating => new() { ValidateScopes = true, ValidateOnBuild = true };

    [Fact]
    public async Task FlattenedSqliteContext_ReplacesTheCoreContextAndKeepsTheCoreMigrator()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:Test"] = "Data Source=:memory:";
        builder.Services.AddModule(NotesModule.Descriptor);
        Configuration(new SqliteProvider()).RegisterSqlite<NotesContextSqlite>(builder);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();

        var context = Assert.IsType<NotesContextSqlite>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.Equal([NotesModule.Key], context.Modules.Modules.Select(module => module.Key));
        Assert.IsType<SqliteCoreDatabaseMigrator>(scope.ServiceProvider.GetRequiredService<ICoreDatabaseMigrator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CoreContextSqlite>());
        Assert.True(scope.ServiceProvider.GetRequiredService<AccessCatalog>().IsKnownGrant("Notes.Read"));
    }

    [Fact]
    public async Task CoreSqliteContext_StaysTheOnlyContextWithoutAProjectContext()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:Test"] = "Data Source=:memory:";
        Configuration(new SqliteProvider()).RegisterSqlite(builder);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();

        Assert.IsType<CoreContextSqlite>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ModuleCatalog>().Modules);
        Assert.False(scope.ServiceProvider.GetRequiredService<AccessCatalog>().IsKnownGrant("Notes.Read"));
    }

    [Fact]
    public async Task FlattenedPostgresContext_IsRegisteredLikeTheSqliteOne()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = PostgresConnection;
        builder.Services.AddModule(NotesModule.Descriptor);
        var database = Configuration(new PostgresProvider());
        database.RegisterSqlite<NotesContextSqlite>(builder);
        database.RegisterPostgres<NotesContextPostgres>(builder);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();

        Assert.IsType<NotesContextPostgres>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.IsType<PostgresCoreDatabaseMigrator>(scope.ServiceProvider.GetRequiredService<ICoreDatabaseMigrator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CoreContextPostgres>());
        Assert.Null(scope.ServiceProvider.GetService<CoreContextSqlite>());
    }

    [Fact]
    public async Task SqliteModuleMigration_AppliesTheModuleMigrationsIntoTheSameDatabaseFile()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = $"Data Source={_file}";
        var database = Configuration(new SqliteProvider());
        database.RegisterSqliteModule<NotesMigrationContext>(builder, NotesModule.Descriptor);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();
        var migrator = Assert.Single(scope.ServiceProvider.GetServices<IModuleMigrator>());

        await migrator.MigrateAsync();

        Assert.Equal(NotesModule.Key, migrator.Module);
        Assert.Empty(await migrator.PendingMigrationsAsync());
        var history = scope.ServiceProvider.GetRequiredService<NotesMigrationContext>().GetService<IHistoryRepository>();
        Assert.Contains(NotesModule.Descriptor.MigrationsHistoryTable, history.GetCreateScript());
    }

    [Fact]
    public async Task PostgresModuleMigration_UsesItsOwnMigrationHistoryTable()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = PostgresConnection;
        Configuration(new PostgresProvider()).RegisterPostgresModule<NotesMigrationContext>(builder, NotesModule.Descriptor);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NotesMigrationContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Contains(NotesModule.Descriptor.MigrationsHistoryTable, context.GetService<IHistoryRepository>().GetCreateScript());
        Assert.Equal(NotesModule.Key, Assert.Single(scope.ServiceProvider.GetServices<IModuleMigrator>()).Module);
    }

    [Fact]
    public async Task ModuleMigration_IsSkippedForTheOtherProvider()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = PostgresConnection;
        var database = Configuration(new PostgresProvider());
        database.RegisterSqliteModule<NotesMigrationContext>(builder, NotesModule.Descriptor);
        await using var services = builder.Services.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();

        Assert.Empty(scope.ServiceProvider.GetServices<IModuleMigrator>());
    }
}

public sealed class NotesContextPostgres(DbContextOptions<NotesContextPostgres> options, ModuleCatalog modules)
    : AbstractCoreContext(options, modules), INotesContext
{
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<NoteLog> NoteLogs => Set<NoteLog>();
}
