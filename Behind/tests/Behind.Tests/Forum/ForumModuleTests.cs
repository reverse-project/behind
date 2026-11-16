using Behind.Common.Database.Raw;
using Behind.Common.Database.Modules;
using Behind.Core.Domain;
using Behind.Forum.Database.Postgres;
using Behind.Forum.Database.Sqlite;
using Behind.Forum.Domain;
using Behind.Forum.WebApi;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.Forum;

public sealed class ForumModuleTests
{
    [Fact]
    public void Descriptor_RegistersGrantsRoutesSettingsAndVersionedEditableEntities()
    {
        var catalog = ModuleCatalog.Create([ForumModule.Descriptor]);
        Assert.True(catalog.IsValid);
        var access = AccessCatalog.From(catalog.GetOrElse(ModuleCatalog.Empty)).GetOrElse(AccessCatalog.Core);
        Assert.True(access.IsKnownGrant("Forum.Read"));
        Assert.True(access.IsKnownGrant("Forum.Manage"));
        Assert.True(access.IsKnownGrant("ForumModeration.Manage"));
        Assert.Contains("forum.json", ForumModule.Descriptor.ConfigurationFiles);
        using var bed = new SportTestBed();
        using var context = bed.Read();

        foreach (var type in new[]
        {
            typeof(ForumBoardCategory), typeof(ForumBoard), typeof(ForumPost), typeof(ForumUserModeration)
        })
        {
            var entity = context.Model.FindEntityType(type)!;
            Assert.Equal(["Key", "Version"], entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
            Assert.True(entity.IsSoftDeletable);
            Assert.Contains(context.RawTables, table => table.EntityName == type.Name);
        }
    }

    [Fact]
    public async Task SqliteMigrations_CreateModuleTablesAndMatchTheModel()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new ForumContextSqlite(new DbContextOptionsBuilder<ForumContextSqlite>()
            .UseSqlite(connection, options => options.MigrationsHistoryTable(ForumModule.Descriptor.MigrationsHistoryTable)).Options);
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        var tables = await context.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();
        Assert.Contains("ForumBoards", tables);
        Assert.Contains("ForumBoardCategories", tables);
        Assert.Contains("ForumPosts", tables);
        Assert.Contains("ForumUserModerations", tables);
        Assert.Contains(ForumModule.Descriptor.MigrationsHistoryTable, tables);
    }

    [Fact]
    public void PostgresMigrations_MatchModelAndHaveTheirOwnHistory()
    {
        using var context = new ForumContextPostgresFactory().CreateDbContext([]);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_InitialForum"));
        Assert.Contains(ForumModule.Descriptor.MigrationsHistoryTable, context.GetService<IHistoryRepository>().GetCreateScript());
    }
}
