using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class MessageMigrationTests
{
    [Fact]
    public async Task Sqlite_UpgradesTheExistingSchemaAndKeepsExistingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261112082917_ProfilesPrivacyAndContentRestrictions");
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);
        var roles = await context.Roles.CountAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(roles, await context.Roles.CountAsync());
        Assert.Empty(await context.Messages.ToListAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        var table = context.Model.FindEntityType(typeof(Message))!;
        Assert.Equal(["Key", "Version"], table.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.DoesNotContain(table.GetProperties(), property => property.Name is "CreatedAt" or "UpdatedAt");
    }

    [Fact]
    public void Postgres_GeneratesTheSchemaWithoutPendingModelChanges()
    {
        using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql("Host=localhost;Database=behind").Options);
        var sql = context.GetService<IMigrator>().GenerateScript(
            "20261112082920_ProfilesPrivacyAndContentRestrictions", "20261112115436_AddMessages");

        Assert.Contains("CREATE TABLE \"Messages\"", sql);
        Assert.Contains("PRIMARY KEY (\"Key\", \"Version\")", sql);
        Assert.Contains("\"StartsAt\" bigint", sql);
        Assert.Contains("\"EndsAt\" bigint", sql);
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
