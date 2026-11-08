using Behind.Common.Database.Modules;
using Behind.Common.Database.Raw;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Sport.Common;
using Behind.Sport.Database;
using Behind.Sport.Database.Commands;
using Behind.Sport.Database.Queries;
using Behind.Sport.Database.Postgres;
using Behind.Sport.Database.Sqlite;
using Behind.Sport.Domain;
using Behind.Sport.WebApi;
using Behind.Web.Database.Postgres;
using Behind.Web.Database.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PostgresProvider = Behind.Core.Common.Configuration.Postgres;
using SqliteProvider = Behind.Core.Common.Configuration.Sqlite;

namespace Behind.Tests.Sport;

public sealed class SportModuleTests : IDisposable
{
    private static readonly string ConnectionString = "Host=localhost;Database=behind-test;Username=test;******";

    private static readonly Type[] Entities =
    [
        typeof(Discipline), typeof(Position), typeof(StatType), typeof(Season),
        typeof(Team), typeof(Player), typeof(PlayerSeason), typeof(Stat)
    ];

    private readonly SportTestBed _bed = new();
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"behind-sport-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        _bed.Dispose();

        foreach (var path in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_file)}*"))
        {
            File.Delete(path);
        }
    }

    private static DatabaseConfiguration Configuration(Provider provider) =>
        new(Some(provider), Some(new Key("Test")));

    private static ServiceProviderOptions Validating => new() { ValidateScopes = true, ValidateOnBuild = true };

    private static AccessCatalog Access() =>
        AccessCatalog.From(ModuleCatalog.Create([SportModule.Descriptor]).GetOrElse(ModuleCatalog.Empty))
            .GetOrElse(AccessCatalog.Core);

    [Fact]
    public void Descriptor_IsAValidModuleWhoseRoutesUseTheModuleControllers()
    {
        Assert.True(ModuleCatalog.Create([SportModule.Descriptor]).IsValid);
        Assert.All(SportModule.Descriptor.Routes, route => Assert.StartsWith("modules/sport/controllers/", route.Controller));
        Assert.Contains(SportModule.Descriptor.Routes, route => route.Path == SportModuleNames.PublicRoute);
        Assert.Contains(SportModule.Descriptor.Routes, route => route.Path == SportModuleNames.ManageRoute);
        Assert.Equal(SportModuleNames.PublicRoute, Assert.Single(SportModule.Descriptor.MenuItems).Url);
        var page = Assert.Single(SportModule.Descriptor.AdministrationPages);
        Assert.Equal((SportModuleNames.ManageRoute, SportModuleNames.WriteGrant), (page.Href, page.Grant));
    }

    [Fact]
    public void Descriptor_CanBeLoadedNextToTheOtherModulesOfTheWebApplication()
    {
        var catalog = ModuleCatalog.Create(
            [Behind.MarkdownViewer.WebApi.MarkdownViewerModule.Descriptor, Behind.Quotes.WebApi.QuotesModule.Descriptor, SportModule.Descriptor]);

        Assert.True(catalog.IsValid);
        Assert.True(AccessCatalog.From(catalog.GetOrElse(ModuleCatalog.Empty)).IsValid);
    }

    [Fact]
    public void Descriptor_DeclaresEveryEntityAsSoftDeleted()
    {
        var database = SportModule.Descriptor.Database.Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal(Entities.Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))), database.SoftDeletedEntities.Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))));
        Assert.Empty(database.ReadOnlyEntities);
    }

    [Fact]
    public void Access_GivesAdministratorsEverythingAndUsersRead()
    {
        var catalog = Access();
        IReadOnlyList<string> Grants(Guid roleId) =>
            catalog.FindRole(roleId).Match(() => [], role => role.Grants.Select(grant => grant.Grant.Name).ToList());

        Assert.All(
            [SportModuleNames.ReadGrant, SportModuleNames.WriteGrant, SportModuleNames.DeleteGrant, "Sport.Manage"],
            grant => Assert.Contains(grant, Grants(BuiltInAccess.Administrators.Id)));
        Assert.Contains(SportModuleNames.ReadGrant, Grants(BuiltInAccess.Users.Id));
        Assert.DoesNotContain(SportModuleNames.WriteGrant, Grants(BuiltInAccess.Users.Id));
    }

    [Fact]
    public void Menu_AddsTheSportPageToTheMainMenu()
    {
        var catalog = ModuleCatalog.Create([SportModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

        var main = Assert.Single(BuiltInMenus.For(catalog), menu => menu.Id == BuiltInMenus.Main.Id);

        Assert.Contains(main.Items, item => item.Id == SportModuleNames.MenuItemId && item.Url == SportModuleNames.PublicRoute);
    }

    [Fact]
    public void Model_KeepsEveryEntityVersionedAndSoftDeletable()
    {
        using var context = _bed.Read();

        Assert.All(Entities, type =>
        {
            var entity = context.Model.FindEntityType(type)!;

            Assert.StartsWith("Sport", entity.GetTableName());
            Assert.Equal(["Key", "Version"], entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
            Assert.True(entity.IsSoftDeletable);
        });
    }

    [Fact]
    public void Context_ExposesItselfAsTheModuleContext()
    {
        using var context = _bed.Read();
        ISportContext module = context;

        Assert.Same(context, module.Context);
        Assert.Same(context.Teams, module.Teams);
        Assert.Same(context.SoftDelete, module.SoftDelete);
    }

    [Fact]
    public void RawTables_ExposeEveryTableOfTheModuleAsEditable()
    {
        using var context = _bed.Read();

        foreach (var name in new[] { "SportDisciplines", "SportPositions", "SportStatTypes", "SportSeasons", "SportTeams", "SportPlayers", "SportPlayerSeasons", "SportStats" })
        {
            var table = Assert.Single(context.RawTables, raw => raw.Name == name);

            Assert.False(context.IsReadOnly(table));
            Assert.DoesNotContain(table.Columns, column => column.IsSensitive);
        }
    }

    [Fact]
    public async Task Registration_FlattensTheSportIntoTheProjectContext()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:Test"] = "Data Source=:memory:";
        builder.Configuration["Backend:DataDirectory"] = Path.GetTempPath();
        builder.Services.AddModule(SportModule.Descriptor).AddSportDatabase().AddVfsContentStore(builder.Configuration);
        Configuration(new SqliteProvider()).RegisterSqlite<WebContextSqlite>(builder);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();

        var context = Assert.IsType<WebContextSqlite>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<ISportContext>());
        Assert.IsType<TeamCommands>(scope.ServiceProvider.GetRequiredService<IVersionedCommands<TeamDraft>>());
        Assert.IsType<PlayerQueries>(scope.ServiceProvider.GetRequiredService<IListingQueries<PlayerListing>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISportDetailQueries>());
        Assert.IsType<ModuleFiles>(scope.ServiceProvider.GetRequiredService<IModuleFiles>());
        Assert.IsType<SportAuditLabelSource>(Assert.Single(scope.ServiceProvider.GetServices<IAuditLabelSource>()));
        Assert.IsType<SportSeed>(Assert.Single(scope.ServiceProvider.GetServices<IModuleSeed>()));
        Assert.True(scope.ServiceProvider.GetRequiredService<AccessCatalog>().IsKnownGrant(SportModuleNames.ReadGrant));
    }

    [Fact]
    public async Task Registration_PostgresProjectContextImplementsTheModuleContext()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = ConnectionString;
        builder.Services.AddModule(SportModule.Descriptor).AddSportDatabase();
        var database = Configuration(new PostgresProvider());
        database.RegisterSqlite<WebContextSqlite>(builder);
        database.RegisterPostgres<WebContextPostgres>(builder);
        await using var services = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = services.CreateAsyncScope();

        Assert.IsType<WebContextPostgres>(scope.ServiceProvider.GetRequiredService<ISportContext>());
    }

    [Fact]
    public async Task Registration_WebApplicationRegistersTheModuleOnce()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddSport().AddSport();

        await using var services = builder.Services.BuildServiceProvider();

        Assert.Equal(SportModule.Descriptor, Assert.Single(services.GetServices<ModuleDescriptor>()));
    }

    [Fact]
    public async Task SqliteMigration_CreatesTheSportTablesInTheSharedDatabaseFile()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = $"Data Source={_file}";
        Configuration(new SqliteProvider()).RegisterSqliteModule<SportContextSqlite>(builder, SportModule.Descriptor);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();
        var migrator = Assert.Single(scope.ServiceProvider.GetServices<IModuleMigrator>());

        await migrator.MigrateAsync();

        Assert.Equal(SportModuleNames.Key, migrator.Module);
        Assert.Empty(await migrator.PendingMigrationsAsync());
        var context = scope.ServiceProvider.GetRequiredService<SportContextSqlite>();
        Assert.Contains(SportModule.Descriptor.MigrationsHistoryTable, context.GetService<IHistoryRepository>().GetCreateScript());
        Assert.False(context.Database.HasPendingModelChanges());
        var tables = await context.Database.SqlQueryRaw<string>("select name as Value from sqlite_master where type = 'table'").ToListAsync();
        Assert.Contains("SportTeams", tables);
        Assert.Contains("SportStats", tables);
    }

    [Fact]
    public async Task PostgresMigration_UsesItsOwnHistoryTableAndTheSameModel()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = ConnectionString;
        Configuration(new PostgresProvider()).RegisterPostgresModule<SportContextPostgres>(builder, SportModule.Descriptor);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SportContextPostgres>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Contains(SportModule.Descriptor.MigrationsHistoryTable, context.GetService<IHistoryRepository>().GetCreateScript());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_CreateSport"));
    }

    [Fact]
    public void DesignTimeFactories_UseTheModuleHistoryTable()
    {
        using var sqlite = new SportContextSqliteFactory().CreateDbContext([]);
        using var postgres = new SportContextPostgresFactory().CreateDbContext([]);

        Assert.Contains(SportModule.Descriptor.MigrationsHistoryTable, sqlite.GetService<IHistoryRepository>().GetCreateScript());
        Assert.Contains(SportModule.Descriptor.MigrationsHistoryTable, postgres.GetService<IHistoryRepository>().GetCreateScript());
    }

    [Fact]
    public async Task ModuleFiles_DelegateToTheVfs()
    {
        var vfs = Substitute.For<IVfsCommands>();
        var vfsQueries = Substitute.For<IVfsQueries>();
        var id = Guid.NewGuid();
        vfs.Add(default!, default).ReturnsForAnyArgs(id);
        vfsQueries.Open(default!, default, default).ReturnsForAnyArgs(Task.FromResult<Option<IVfsQueries.Content>>(None));
        await using var context = _bed.Read();
        var files = new ModuleFiles(context, vfs, vfsQueries);

        Assert.Equal(id, await files.Add(new IModuleFiles.Upload("T", "f.png", "image/png", [1])));
        await files.Attach("T", id, [id]);
        Assert.True((await files.Open("T", id)).IsNone());
        Assert.False(await files.Exists("T", id));
        await vfs.Received(1).Attach("T", id, Arg.Is<IReadOnlyCollection<Guid>>(keys => keys.Single() == id), Arg.Any<CancellationToken>());
    }
}
