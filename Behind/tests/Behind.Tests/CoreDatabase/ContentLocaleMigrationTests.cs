using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Forum.Database.Sqlite;
using Behind.Forum.WebApi;
using Behind.Quotes.Database.Sqlite;
using Behind.Quotes.WebApi;
using Behind.Sport.Database.Sqlite;
using Behind.Sport.Domain;
using Behind.Sport.WebApi;
using Behind.Sites.FSE.Database.Sqlite;
using Behind.Web.Database.Sqlite;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class ContentLocaleMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteFreshAndExistingInstallationsPersistLocaleMetadata(bool upgrade)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        if (upgrade)
        {
            await context.GetService<IMigrator>().MigrateAsync("20261112115426_AddMessages");
            var legacy = new RouteAlias("legacy", "#/news");
            context.RouteAliases.Add(legacy);
            context.Entry(legacy).StampVersionedKey(Guid.NewGuid(), 1);
            await context.SaveChangesAsync();
        }
        await context.Database.MigrateAsync();
        var id = Guid.NewGuid();
        await context.StageContentLocale(nameof(Site), id, "hu-HU");
        await context.SaveChangesAsync();
        Assert.Equal("hu-HU", await context.FindContentLocale(nameof(Site), id));
        Assert.Equal(upgrade ? 1 : 0, await context.RouteAliases.CountAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void PostgreSqlMigrationAndSnapshotContainVersionedIndexedMetadata()
    {
        using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql("Host=localhost;Database=unused").Options);
        var script = context.GetService<IMigrator>().GenerateScript(
            "20261112115436_AddMessages", "20261112195911_ContentLocales");
        Assert.Contains("CREATE TABLE \"ContentLocales\"", script);
        Assert.Contains("PRIMARY KEY (\"Key\", \"Version\")", script);
        Assert.Contains("IX_ContentLocales_EntityType_EntityId_Locale", script);
        Assert.False(context.Database.HasPendingModelChanges());
        context.IncludeAllContentLocales = false;
        context.HiddenLocaleContentKeys = ["Site:" + Guid.NewGuid()];
        Assert.Contains("lower", context.Sites.ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FlattenedWebAndFseInstallationsReuseTheMigratedCoreLocaleTable(bool web)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using (var core = new CoreContextSqlite(new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options))
        {
            await core.Database.MigrateAsync();
        }
        await using (var sport = new SportContextSqlite(new DbContextOptionsBuilder<SportContextSqlite>()
            .UseSqlite(connection, provider => provider.MigrationsHistoryTable(SportModule.Descriptor.MigrationsHistoryTable)).Options))
        {
            await sport.Database.MigrateAsync();
        }
        if (web)
        {
            await using var quotes = new QuotesContextSqlite(new DbContextOptionsBuilder<QuotesContextSqlite>()
                .UseSqlite(connection, provider => provider.MigrationsHistoryTable(QuotesModule.Descriptor.MigrationsHistoryTable)).Options);
            await quotes.Database.MigrateAsync();
            await using var forum = new ForumContextSqlite(new DbContextOptionsBuilder<ForumContextSqlite>()
                .UseSqlite(connection, provider => provider.MigrationsHistoryTable(ForumModule.Descriptor.MigrationsHistoryTable)).Options);
            await forum.Database.MigrateAsync();
        }
        var catalog = ModuleCatalog.Create(web
            ? [SportModule.Descriptor, QuotesModule.Descriptor, ForumModule.Descriptor]
            : [SportModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);
        await using AbstractCoreContext shared = web
            ? new WebContextSqlite(new DbContextOptionsBuilder<WebContextSqlite>().UseSqlite(connection).Options, catalog)
            : new FseContextSqlite(new DbContextOptionsBuilder<FseContextSqlite>().UseSqlite(connection).Options, catalog);
        var id = Guid.NewGuid();
        var discipline = new Discipline("Translated", "shared");
        shared.Set<Discipline>().Add(discipline);
        shared.Entry(discipline).StampVersionedKey(id, 1);
        await shared.StageContentLocale(nameof(Discipline), id, "hu-HU");
        await shared.SaveChangesAsync();
        Assert.Equal("hu-HU", await shared.FindContentLocale(nameof(Discipline), id));
        shared.HiddenLocaleContentKeys = await shared.HiddenForContentLocale(
            [new(nameof(Discipline), id, "hu-HU")], null);
        shared.IncludeAllContentLocales = false;
        Assert.Empty(await shared.Set<Discipline>().ToListAsync());
    }
}
