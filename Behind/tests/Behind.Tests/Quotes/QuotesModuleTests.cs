using System.Text.Json;
using Behind.Common.Database.Modules;
using Behind.Common.Database.Raw;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Quotes.Common;
using Behind.Quotes.Database;
using Behind.Quotes.Database.Postgres;
using Behind.Quotes.Database.Sqlite;
using Behind.Quotes.Domain;
using Behind.Quotes.WebApi;
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

namespace Behind.Tests.Quotes;

public sealed class QuotesModuleTests : IDisposable
{
    private static readonly string ConnectionString = "Host=localhost;Database=behind-test;Username=test;Password=test";

    private readonly QuotesTestBed _bed = new();
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"behind-quotes-{Guid.NewGuid():N}.db");

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

    private static T Required<T>(Option<T> option) =>
        option.Match<T>(() => throw new InvalidOperationException("The option is empty."), value => value);

    private static AccessCatalog Access() =>
        AccessCatalog.From(ModuleCatalog.Create([QuotesModule.Descriptor]).GetOrElse(ModuleCatalog.Empty))
            .GetOrElse(AccessCatalog.Core);

    [Fact]
    public void Descriptor_IsAValidModule()
    {
        var catalog = ModuleCatalog.Create([QuotesModule.Descriptor]);

        Assert.True(catalog.IsValid);
        Assert.Equal(QuotesModuleNames.Key, QuotesModule.Descriptor.Key);
    }

    [Fact]
    public void Descriptor_RoutesAndAdministrationPageUseTheModuleNames()
    {
        var descriptor = QuotesModule.Descriptor;

        Assert.Equal(
            [QuotesModuleNames.PublicRoute, QuotesModuleNames.ManageRoute],
            descriptor.Routes.Select(route => route.Path));
        Assert.All(descriptor.Routes, route => Assert.StartsWith("modules/quotes/controllers/", route.Controller));
        var page = Assert.Single(descriptor.AdministrationPages);
        Assert.Equal((QuotesModuleNames.ManageRoute, QuotesModuleNames.WriteGrant), (page.Href, page.Grant));
        Assert.Equal(QuotesModuleNames.PublicRoute, Assert.Single(descriptor.MenuItems).Url);
    }

    [Fact]
    public void Descriptor_DeclaresTheQuoteAsSoftDeletedAndTheFrontendOfTheModule()
    {
        var database = Required(QuotesModule.Descriptor.Database);

        Assert.Equal([typeof(Quote)], database.SoftDeletedEntities);
        Assert.Empty(database.ReadOnlyEntities);
        Assert.Equal(typeof(QuotesModule).Assembly, Required(QuotesModule.Descriptor.Frontend).Assembly);
    }

    [Fact]
    public void Access_GivesAdministratorsEverythingAndUsersRead()
    {
        var catalog = Access();
        IReadOnlyList<string> Grants(Guid roleId) =>
            catalog.FindRole(roleId).Match(() => [], role => role.Grants.Select(grant => grant.Grant.Name).ToList());

        Assert.All(
            [QuotesModuleNames.ReadGrant, QuotesModuleNames.WriteGrant, QuotesModuleNames.DeleteGrant, "Quotes.Manage"],
            grant =>
            {
                Assert.True(catalog.IsKnownGrant(grant));
                Assert.Contains(grant, Grants(BuiltInAccess.Administrators.Id));
            });
        Assert.Contains(QuotesModuleNames.ReadGrant, Grants(BuiltInAccess.Users.Id));
        Assert.DoesNotContain(QuotesModuleNames.WriteGrant, Grants(BuiltInAccess.Users.Id));
    }

    [Fact]
    public void Menu_AddsTheQuotesPageToTheMainMenu()
    {
        var catalog = ModuleCatalog.Create([QuotesModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

        var main = Assert.Single(BuiltInMenus.For(catalog), menu => menu.Id == BuiltInMenus.Main.Id);

        Assert.Contains(main.Items, item => item.Id == QuotesModuleNames.MenuItemId && item.Url == QuotesModuleNames.PublicRoute);
    }

    [Fact]
    public void Model_ContainsTheQuoteWithAVersionedKeyAndSoftDelete()
    {
        using var context = _bed.Read();
        var entity = context.Model.FindEntityType(typeof(Quote))!;

        Assert.Equal("Quotes", entity.GetTableName());
        Assert.Equal(["Key", "Version"], entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.True(entity.IsSoftDeletable);
    }

    [Fact]
    public void Context_ExposesItselfAsTheModuleContext()
    {
        using var context = _bed.Read();
        IQuotesContext module = context;

        Assert.Same(context, module.Context);
        Assert.Same(context.Quotes, module.Quotes);
        Assert.Same(context.SoftDelete, module.SoftDelete);
    }

    [Fact]
    public async Task Context_FiltersByTheDeletionStateThroughTheModuleContext()
    {
        var kept = await _bed.CreateAsync("Kept", "Ada");
        var removed = await _bed.CreateAsync("Removed", "Ada");
        await _bed.WithCommandsAsync(commands => commands.Delete(removed));
        await using var context = _bed.Read();
        IModuleContext module = context;
        using var scope = context.SoftDelete.IncludeDeleted();

        var active = await module.InDeletionState(context.Quotes, DeletionState.Active).ToListAsync();
        var deleted = await module.InDeletionState(context.Quotes, DeletionState.Deleted).ToListAsync();

        Assert.Equal([kept], active.Select(quote => context.Entry(quote).Property<Guid>(VersionedColumns.Key).CurrentValue));
        Assert.Equal([removed], deleted.Select(quote => context.Entry(quote).Property<Guid>(VersionedColumns.Key).CurrentValue));
    }

    [Fact]
    public void RawTables_ExposeTheQuotesTableOfTheModule()
    {
        using var context = _bed.Read();
        var table = Assert.Single(context.RawTables, raw => raw.Name == "Quotes");

        Assert.Equal("Quote", table.EntityName);
        Assert.Equal(["Key", "Version"], table.KeyColumns.Select(column => column.Name));
        Assert.False(context.IsReadOnly(table));
        Assert.DoesNotContain(table.Columns, column => column.IsSensitive);
    }

    [Fact]
    public async Task RawDatabase_InsertsAQuoteThatTheModuleThenLists()
    {
        var key = Guid.NewGuid();
        await using var context = _bed.Write();
        var commands = new RawDatabaseCommands(context, new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System);

        var result = await commands.Insert(
            "Quotes",
            new Dictionary<string, JsonElement>
            {
                ["Key"] = JsonSerializer.SerializeToElement(key),
                ["Version"] = JsonSerializer.SerializeToElement(1),
                ["Text"] = JsonSerializer.SerializeToElement("Raw text"),
                ["Author"] = JsonSerializer.SerializeToElement("Raw author")
            });

        Assert.IsType<RawWriteResult.Saved>(result);
        Assert.Equal([key], (await _bed.ListAsync()).Select(quote => quote.Id.Key));
    }

    [Fact]
    public async Task Registration_FlattensTheQuotesIntoTheProjectContext()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:Test"] = "Data Source=:memory:";
        builder.Services.AddModule(QuotesModule.Descriptor).AddQuotesDatabase();
        Configuration(new SqliteProvider()).RegisterSqlite<WebContextSqlite>(builder);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();

        var context = Assert.IsType<WebContextSqlite>(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>());
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<IQuotesContext>());
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.IsType<SqliteCoreDatabaseMigrator>(scope.ServiceProvider.GetRequiredService<ICoreDatabaseMigrator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IQuoteCommands>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IQuoteQueries>());
        Assert.IsType<QuotesAuditLabelSource>(Assert.Single(scope.ServiceProvider.GetServices<IAuditLabelSource>()));
        Assert.IsType<QuotesSeed>(Assert.Single(scope.ServiceProvider.GetServices<IModuleSeed>()));
        Assert.True(scope.ServiceProvider.GetRequiredService<AccessCatalog>().IsKnownGrant(QuotesModuleNames.ReadGrant));
    }

    [Fact]
    public async Task Registration_PostgresProjectContextImplementsTheModuleContext()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = ConnectionString;
        builder.Services.AddModule(QuotesModule.Descriptor).AddQuotesDatabase();
        var database = Configuration(new PostgresProvider());
        database.RegisterSqlite<WebContextSqlite>(builder);
        database.RegisterPostgres<WebContextPostgres>(builder);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();

        Assert.IsType<WebContextPostgres>(scope.ServiceProvider.GetRequiredService<IQuotesContext>());
    }

    [Fact]
    public async Task Registration_WebApplicationRegistersTheModuleOnce()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddQuotes().AddQuotes();

        await using var services = builder.Services.BuildServiceProvider();

        Assert.Equal(QuotesModule.Descriptor, Assert.Single(services.GetServices<ModuleDescriptor>()));
    }

    [Fact]
    public async Task SqliteMigration_CreatesTheQuotesTableInTheSharedDatabaseFile()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = $"Data Source={_file}";
        Configuration(new SqliteProvider()).RegisterSqliteModule<QuotesContextSqlite>(builder, QuotesModule.Descriptor);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();
        var migrator = Assert.Single(scope.ServiceProvider.GetServices<IModuleMigrator>());

        await migrator.MigrateAsync();

        Assert.Equal(QuotesModuleNames.Key, migrator.Module);
        Assert.Empty(await migrator.PendingMigrationsAsync());
        var context = scope.ServiceProvider.GetRequiredService<QuotesContextSqlite>();
        Assert.Contains(QuotesModule.Descriptor.MigrationsHistoryTable, context.GetService<IHistoryRepository>().GetCreateScript());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains(
            "Quotes",
            await context.Database.SqlQueryRaw<string>("select name as Value from sqlite_master where type = 'table'").ToListAsync());
    }

    [Fact]
    public async Task PostgresMigration_UsesItsOwnHistoryTableAndTheSameModel()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Test"] = ConnectionString;
        Configuration(new PostgresProvider()).RegisterPostgresModule<QuotesContextPostgres>(builder, QuotesModule.Descriptor);
        await using var services = builder.Services.BuildServiceProvider(Validating);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuotesContextPostgres>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Contains(QuotesModule.Descriptor.MigrationsHistoryTable, context.GetService<IHistoryRepository>().GetCreateScript());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_CreateQuotes"));
    }

    [Fact]
    public void DesignTimeFactories_UseTheModuleHistoryTable()
    {
        using var sqlite = new QuotesContextSqliteFactory().CreateDbContext([]);
        using var postgres = new QuotesContextPostgresFactory().CreateDbContext([]);

        Assert.Contains(QuotesModule.Descriptor.MigrationsHistoryTable, sqlite.GetService<IHistoryRepository>().GetCreateScript());
        Assert.Contains(QuotesModule.Descriptor.MigrationsHistoryTable, postgres.GetService<IHistoryRepository>().GetCreateScript());
    }
}
