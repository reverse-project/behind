using Behind.Common.Database.Modules;
using Behind.Common.Database.Raw;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Forum.Database.Postgres;
using Behind.Forum.Database.Sqlite;
using Behind.Forum.WebApi;
using Behind.Quotes.WebApi;
using Behind.Sport.WebApi;
using Behind.Web.Database.Postgres;
using Behind.Web.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class ProfileMigrationTests
{
    private static readonly ModuleCatalog Catalog = ModuleCatalog.Create(
        [ForumModule.Descriptor, QuotesModule.Descriptor, SportModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    [Fact]
    public async Task Sqlite_UpgradesLegacyUsersAndLoadsCoreAndForumInTheFlattenedContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var id = Guid.NewGuid();
        await using (var core = new CoreContextSqlite(
                         new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options))
        {
            await core.GetService<IMigrator>().MigrateAsync("20261111202903_AddChatMessages");
            await core.Database.ExecuteSqlAsync(
                $"INSERT INTO Users (Id, DisplayName, ProfilePictureId) VALUES ({id}, {"Legacy"}, NULL)");
            await core.Database.MigrateAsync();
            Assert.False(core.Database.HasPendingModelChanges());
            var user = await core.Users.SingleAsync();
            Assert.Null(user.Birthday);
            Assert.True(user.HideEmail);
            Assert.Equal("Legacy", user.DisplayName);
            var table = Assert.Single(core.RawTables, table => table.Name == "Users");
            Assert.True(Assert.Single(table.Columns, column => column.Name == "Birthday").IsSensitive);
            Assert.False(Assert.Single(table.Columns, column => column.Name == "HideEmail").IsSensitive);
        }

        await using (var forum = new ForumContextSqlite(
                         new DbContextOptionsBuilder<ForumContextSqlite>().UseSqlite(connection,
                             options => options.MigrationsHistoryTable(ForumModule.Descriptor.MigrationsHistoryTable)).Options))
        {
            await forum.Database.MigrateAsync();
            Assert.False(forum.Database.HasPendingModelChanges());
        }

        await using var flattened = new WebContextSqlite(
            new DbContextOptionsBuilder<WebContextSqlite>().UseSqlite(connection).Options, Catalog);
        Assert.True((await flattened.Users.SingleAsync()).HideEmail);
        Assert.Empty(await flattened.ContentRestrictions.ToListAsync());
        Assert.Empty(await flattened.NewsComments.Where(comment => comment.IsAnonymous).ToListAsync());
        Assert.Empty(await flattened.ChatMessages.Where(message => message.IsAnonymous).ToListAsync());
        Assert.Empty(await flattened.ForumPosts.Where(post => post.IsAnonymous).ToListAsync());
    }

    [Fact]
    public void Postgres_ScriptsBothChainsAndMatchesTheFlattenedProfileModel()
    {
        using var core = new CoreContextPostgresFactory().CreateDbContext([]);
        using var forum = new ForumContextPostgresFactory().CreateDbContext([]);
        using var flattened = new WebContextPostgres(
            new DbContextOptionsBuilder<WebContextPostgres>()
                .UseNpgsql("Host=localhost;Database=behind").Options, Catalog);
        var sql = core.GetService<IMigrator>().GenerateScript("20261111202906_AddChatMessages");
        Assert.Contains("ADD \"Birthday\" date", sql);
        Assert.Contains("ADD \"HideEmail\" boolean NOT NULL DEFAULT TRUE", sql);
        Assert.Contains("CREATE TABLE \"ContentRestrictions\"", sql);
        Assert.Contains("ALTER TABLE \"NewsComments\" ADD \"IsAnonymous\" boolean NOT NULL DEFAULT FALSE", sql);
        Assert.Contains("ALTER TABLE \"ChatMessages\" ADD \"IsAnonymous\" boolean NOT NULL DEFAULT FALSE", sql);
        Assert.Contains("ALTER TABLE \"ForumPosts\" ADD \"IsAnonymous\" boolean NOT NULL DEFAULT FALSE",
            forum.GetService<IMigrator>().GenerateScript("20261111193458_AddBoardCategories"));
        Assert.False(core.Database.HasPendingModelChanges());
        Assert.False(forum.Database.HasPendingModelChanges());
        Assert.Equal("date", flattened.Model.FindEntityType(typeof(HumanUser))!
            .FindProperty(nameof(HumanUser.Birthday))!.GetColumnType());
        Assert.Equal(true, flattened.Model.FindEntityType(typeof(HumanUser))!
            .FindProperty(nameof(HumanUser.HideEmail))!.GetDefaultValue());
    }
}
