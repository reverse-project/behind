using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class OidcMigrationTests
{
    [Fact]
    public async Task SqliteUpgradePreservesExistingManualMemberships()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261115232156_AddSchedules");
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "migration@test.com");
        var roleId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO Roles (Id, Name, IsDeveloper) VALUES ({roleId}, {"Existing manual role"}, {false})");
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO UserRoles (UserId, RoleId) VALUES ({userId}, {roleId})");

        await context.Database.MigrateAsync();

        var membership = await context.Set<Dictionary<string, object>>("UserRoles").SingleAsync();
        Assert.True((bool)membership["IsManuallyAssigned"]);
        Assert.Empty(await context.Set<Dictionary<string, object>>("OidcAccountBindings").ToListAsync());
        Assert.Empty(await context.Set<Dictionary<string, object>>("OidcRoleOwnerships").ToListAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void PostgresMigrationMatchesModelAndCreatesUniqueBindingAndOwnershipKeys()
    {
        using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql("Host=localhost;Database=behind").Options);
        var sql = context.GetService<IMigrator>().GenerateScript(
            "20261115232216_AddSchedules", "20261116103140_PersistOidcAccounts");

        Assert.Contains("CREATE TABLE \"OidcAccountBindings\"", sql);
        Assert.Contains("CREATE TABLE \"OidcRoleOwnerships\"", sql);
        Assert.Contains("\"IsManuallyAssigned\" boolean NOT NULL DEFAULT TRUE", sql);
        Assert.Contains("PRIMARY KEY (\"ProviderDigest\", \"IssuerDigest\", \"SubjectDigest\")", sql);
        Assert.False(context.Database.HasPendingModelChanges());
    }

}
