using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class DeveloperConfigurationManageMigrationTests : IDisposable
{
    private const string BeforeMigration = "20261105190631_AddTwoFactor";
    private const string ConfigurationManage = "Configuration.Manage";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public DeveloperConfigurationManageMigrationTests()
    {
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CoreContextSqlite CreateContext() =>
        new(new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(_connection).Options);

    private async Task SeedOutdatedDevelopersAsync()
    {
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(BeforeMigration);
        await context.EnsureBuiltInRolesAsync();

        var configurationManage = BuiltInAccess.Grants.Single(grant => grant.Grant.Name == ConfigurationManage);
        var roleGrants = context.Set<Dictionary<string, object>>("RoleGrants");
        roleGrants.RemoveRange(await roleGrants
            .Where(roleGrant => EF.Property<Guid>(roleGrant, "RoleId") == BuiltInAccess.Developers.Id
                                && EF.Property<Guid>(roleGrant, "GrantId") == configurationManage.Id)
            .ToListAsync());
        await context.SaveChangesAsync();
    }

    private async Task<List<string>> DeveloperGrantNamesAsync()
    {
        await using var context = CreateContext();

        return await context.Database
            .SqlQuery<string>(
                $"""
                 SELECT g."Name" AS "Value" FROM "RoleGrants" rg
                 JOIN "Grants" g ON g."Id" = rg."GrantId"
                 WHERE rg."RoleId" = {BuiltInAccess.Developers.Id}
                 ORDER BY g."Name"
                 """)
            .ToListAsync();
    }

    [Fact]
    public async Task Migrate_GivesTheDevelopersRoleConfigurationManage()
    {
        await SeedOutdatedDevelopersAsync();
        Assert.DoesNotContain(ConfigurationManage, await DeveloperGrantNamesAsync());

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.Contains(ConfigurationManage, await DeveloperGrantNamesAsync());
    }

    [Fact]
    public async Task Migrate_LeavesTheDevelopersRoleWithOnlyTheDeveloperGrants()
    {
        await SeedOutdatedDevelopersAsync();

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.Equal(["Configuration.Manage", "Configuration.Read", "RawDatabase.Manage"], await DeveloperGrantNamesAsync());
    }

    [Fact]
    public async Task Migrate_DoesNotDuplicateTheGrantWhenTheDevelopersRoleAlreadyHasIt()
    {
        await using (var context = CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(BeforeMigration);
            await context.EnsureBuiltInRolesAsync();
            await context.Database.MigrateAsync();
        }

        Assert.Single(await DeveloperGrantNamesAsync(), name => name == ConfigurationManage);
    }
}
