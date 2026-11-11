using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class ChatMigrationTests
{
    [Fact]
    public async Task Sqlite_MigratesAnExistingDatabaseAndExposesTheChatTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261111181101_AddProfilePicture");
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);
        await context.Database.MigrateAsync();
        Assert.Empty(await context.ChatMessages.ToListAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        var table = context.Model.FindEntityType(typeof(ChatMessage))!;
        Assert.Equal(["Key", "Version"], table.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.DoesNotContain(table.GetProperties(), property => property.Name is "CreatedAt" or "UpdatedAt");
    }

    [Fact]
    public void Postgres_GeneratesTheChatTableAndHasNoPendingModelChanges()
    {
        using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql("Host=localhost;Database=behind").Options);
        var sql = context.GetService<IMigrator>().GenerateScript(
            "20261111181039_AddProfilePicture", "20261111202906_AddChatMessages");
        Assert.Contains("CREATE TABLE \"ChatMessages\"", sql);
        Assert.Contains("PRIMARY KEY (\"Key\", \"Version\")", sql);
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
