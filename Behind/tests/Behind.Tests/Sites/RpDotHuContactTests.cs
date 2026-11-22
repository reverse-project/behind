using System.Text.Json;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Contact.Database;
using Behind.Contact.Database.Commands;
using Behind.Contact.Domain;
using Behind.Core.Database;
using Behind.Sites.RPDotHu;
using Behind.Sites.RPDotHu.Database.Migrator;
using Behind.Sites.RPDotHu.Database.Postgres;
using Behind.Sites.RPDotHu.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Behind.Tests.Sites;

public sealed class RpDotHuContactTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SiteRegistersContactRoutesAndSharedContext(bool postgres)
    {
        using var configuration = new TemporarySiteConfiguration("rphu");
        if (postgres)
        {
            configuration.Write("database.json", JsonSerializer.Serialize(new
            {
                ConnectionStrings = new { DefaultConnection = "Host=localhost;Database=rpdothu;Username=test;Password=test" },
                Database = new { Provider = "Postgres", Key = "DefaultConnection" }
            }));
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = configuration.ConfigDirectory });
        await using var app = builder.AddRpDotHu().Build();
        app.UseRpDotHu();
        await using var scope = app.Services.CreateAsyncScope();

        Assert.Contains(app.Services.GetRequiredService<ModuleCatalog>().Modules, module => module.Key == "contact");
        var context = scope.ServiceProvider.GetRequiredService<AbstractCoreContext>();
        if (postgres)
        {
            Assert.IsType<RpDotHuContextPostgres>(context);
        }
        else
        {
            Assert.IsType<RpDotHuContextSqlite>(context);
        }
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<IContactContext>());
        Assert.NotNull(context.Model.FindEntityType(typeof(ContactForm)));
        Assert.NotNull(context.Model.FindEntityType(typeof(ContactRequest)));
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();
        Assert.Contains(routes, route => route.RoutePattern.RawText == "/contact/forms");
        Assert.Contains(routes, route => route.RoutePattern.RawText == "/contact/manage/forms");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteMigratorAddsContactAndPreservesExistingData(bool upgrade)
    {
        using var configuration = new TemporarySiteConfiguration("rphu");
        await VerifyMigrator(configuration, upgrade, false);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgresMigratorAddsContactAndPreservesExistingData(bool upgrade)
    {
        using var settings = new ConfigurationManager();
        settings.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = settings["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image must be configured.");
        await using var postgres = new PostgreSqlBuilder(image).WithLabel("behind.test", "rpdothu-contact").Build();
        await postgres.StartAsync();
        using var configuration = new TemporarySiteConfiguration("rphu");
        configuration.Write("database.json", JsonSerializer.Serialize(new
        {
            ConnectionStrings = new { DefaultConnection = postgres.GetConnectionString() },
            Database = new { Provider = "Postgres", Key = "DefaultConnection" }
        }));
        await VerifyMigrator(configuration, upgrade, true);
    }

    private static async Task VerifyMigrator(TemporarySiteConfiguration configuration, bool upgrade, bool postgres)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = configuration.ConfigDirectory });
        using var host = builder.AddRpDotHuMigrator().Build();
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var core = services.GetRequiredService<ICoreDatabaseMigrator>();
        var module = Assert.Single(services.GetServices<IModuleMigrator>());
        var context = services.GetRequiredService<AbstractCoreContext>();
        Assert.Same(context, services.GetRequiredService<IContactContext>());
        if (postgres)
        {
            Assert.IsType<RpDotHuContextPostgres>(context);
        }
        else
        {
            Assert.IsType<RpDotHuContextSqlite>(context);
        }

        await core.MigrateAsync();
        if (upgrade)
        {
            await context.Database.ExecuteSqlRawAsync("CREATE TABLE \"ExistingSiteData\" (\"Value\" INTEGER NOT NULL)");
            await context.Database.ExecuteSqlRawAsync("INSERT INTO \"ExistingSiteData\" (\"Value\") VALUES (42)");
        }
        await module.MigrateAsync();
        Assert.Equal("contact", module.Module);
        Assert.Empty(await module.PendingMigrationsAsync());
        await services.GetRequiredService<AbstractVersioningContext>().Database.MigrateAsync();
        await context.EnsureBuiltInUsersAsync();
        var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = configuration.ConfigDirectory });
        await using var app = webBuilder.AddRpDotHu().Build();
        await using var webScope = app.Services.CreateAsyncScope();
        var webServices = webScope.ServiceProvider;
        var webContext = webServices.GetRequiredService<AbstractCoreContext>();
        var draft = new ContactForm("Help", "Contact", "", "freeText", ContactSubjects.From([]), ContactFieldSchema.From([]));
        var before = DateTimeOffset.UtcNow;
        Assert.IsType<VersionedSaveResult.Saved>(await webServices.GetRequiredService<ContactFormCommands>().Create(draft));
        var after = DateTimeOffset.UtcNow;
        webContext.ChangeTracker.Clear();
        var audit = Assert.Single(await webContext.AuditLogEntries.Where(entry => entry.EntityName == nameof(ContactForm)).ToListAsync());
        Assert.Equal(TimeSpan.Zero, audit.Timestamp.Offset);
        Assert.InRange(audit.Timestamp, before, after);
        await module.MigrateAsync();
        Assert.Single(await ((IContactContext)context).ContactForms.ToListAsync());
        if (upgrade)
        {
            Assert.Equal(42, await context.Database.SqlQueryRaw<int>("SELECT \"Value\" FROM \"ExistingSiteData\"").SingleAsync());
        }
    }
}
