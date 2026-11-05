using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class ImpersonationGrantsMigrationTests : IDisposable
{
    private const string BeforeMigration = "20261105200000_GrantDevelopersConfigurationManage";
    private static readonly Guid RoleId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ImpersonationGrantsMigrationTests()
    {
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CoreContextSqlite CreateContext() =>
        new(new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(_connection).Options);

    private async Task SeedLegacyImpersonationGrantsAsync()
    {
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(BeforeMigration);
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");

        foreach (var (number, action) in new[] { (1, "Read"), (2, "Write"), (3, "Delete"), (4, "Manage") })
        {
            var grantId = Guid.Parse($"60000000-0000-0000-0000-{50 + number:D12}");
            var restrictionId = Guid.Parse($"70000000-0000-0000-0000-{50 + number:D12}");
            var name = $"Impersonation.{action}";

            await context.Database.ExecuteSqlAsync($"""INSERT INTO "Grants" ("Id", "Name") VALUES ({grantId}, {name})""");
            await context.Database.ExecuteSqlAsync($"""INSERT INTO "Restrictions" ("Id", "Name") VALUES ({restrictionId}, {name})""");
            await context.Database.ExecuteSqlAsync($"""INSERT INTO "RoleGrants" ("RoleId", "GrantId") VALUES ({RoleId}, {grantId})""");
            await context.Database.ExecuteSqlAsync($"""INSERT INTO "UserGrants" ("UserId", "GrantId") VALUES ({UserId}, {grantId})""");
            await context.Database.ExecuteSqlAsync($"""INSERT INTO "UserRestrictions" ("UserId", "RestrictionId") VALUES ({UserId}, {restrictionId})""");
        }
    }

    private async Task<List<string>> NamesAsync(string table)
    {
        await using var context = CreateContext();

        return await context.Database
            .SqlQueryRaw<string>($"""SELECT "Name" AS "Value" FROM "{table}" WHERE "Name" LIKE 'Impersonation.%' ORDER BY "Name" """)
            .ToListAsync();
    }

    [Fact]
    public async Task Migrate_KeepsOnlyTheImpersonationManageGrantAndRestriction()
    {
        await SeedLegacyImpersonationGrantsAsync();

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.Equal(["Impersonation.Manage"], await NamesAsync("Grants"));
        Assert.Equal(["Impersonation.Manage"], await NamesAsync("Restrictions"));
    }

    [Fact]
    public async Task Migrate_RemovesTheRemovedGrantsFromRolesAndUsersButKeepsManage()
    {
        await SeedLegacyImpersonationGrantsAsync();

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var verify = CreateContext();
        var roleGrants = await verify.Database.SqlQuery<int>($"""SELECT COUNT(*) AS "Value" FROM "RoleGrants" WHERE "RoleId" = {RoleId}""").SingleAsync();
        var userGrants = await verify.Database.SqlQuery<int>($"""SELECT COUNT(*) AS "Value" FROM "UserGrants" WHERE "UserId" = {UserId}""").SingleAsync();
        var userRestrictions = await verify.Database.SqlQuery<int>($"""SELECT COUNT(*) AS "Value" FROM "UserRestrictions" WHERE "UserId" = {UserId}""").SingleAsync();

        Assert.Equal(1, roleGrants);
        Assert.Equal(1, userGrants);
        Assert.Equal(1, userRestrictions);
    }
}
