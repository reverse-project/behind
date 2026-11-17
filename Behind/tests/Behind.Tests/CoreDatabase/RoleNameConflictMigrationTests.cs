using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

public sealed class RoleNameConflictMigrationTests
{
    [Fact]
    public async Task SqliteUpgradeReportsConflictsAndPreservesReferencesUntilExplicitRename()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261116103131_PersistOidcAccounts");
        await VerifyConflict(context);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresUpgradeReportsConflictsAndPreservesReferencesUntilExplicitRename()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image must be configured.")).Build();
        await postgres.StartAsync();
        await using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261116103140_PersistOidcAccounts");
        await VerifyConflict(context);
    }

    private static async Task VerifyConflict(AbstractCoreContext context)
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var menu = Guid.NewGuid();
        var item = Guid.NewGuid();
        var grant = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "Roles" ("Id", "Name", "IsDeveloper") VALUES ({first}, {"Editors"}, {false}), ({second}, {"Editors"}, {true});
            INSERT INTO "Grants" ("Id", "Name") VALUES ({grant}, {"Test.Read"});
            INSERT INTO "RoleGrants" ("RoleId", "GrantId") VALUES ({first}, {grant});
            INSERT INTO "Menus" ("Key", "Version", "Name", "Title") VALUES ({menu}, {1L}, {"test"}, {"Test"});
            INSERT INTO "MenuItems" ("Key", "Version", "MenuId", "Position", "Title", "Kind", "Target", "Url")
                VALUES ({item}, {1L}, {menu}, {0}, {"Test"}, {"link"}, {"self"}, {"#/test"});
            INSERT INTO "MenuItemRoles" ("MenuItemKey", "MenuItemVersion", "RoleId") VALUES ({item}, {1L}, {second});
            """);
        var user = new Behind.Core.Domain.HumanUser("Conflict test", [new Behind.Core.Domain.Email("conflict@example.test")], None);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var userId = context.Entry(user).Property<Guid>("Id").CurrentValue;
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "UserRoles" ("UserId", "RoleId", "IsManuallyAssigned") VALUES ({userId}, {first}, {true});
            """);

        var error = await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => context.Database.MigrateAsync());
        Assert.Contains("Role name conflicts", error.Message);
        Assert.Contains("Editors", error.Message);
        Assert.Contains(first.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Equal(first, (Guid)Assert.Single(await context.Set<Dictionary<string, object>>("UserRoles").ToListAsync())["RoleId"]);
        Assert.Equal(first, (Guid)Assert.Single(await context.Set<Dictionary<string, object>>("RoleGrants").ToListAsync())["RoleId"]);
        Assert.Equal(second, (Guid)Assert.Single(await context.Set<Dictionary<string, object>>("MenuItemRoles").ToListAsync())["RoleId"]);
        await context.Database.ExecuteSqlAsync($"UPDATE \"Roles\" SET \"Name\" = {"Distinct editors"} WHERE \"Id\" = {second}");
        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.True(await context.Roles.Where(role => EF.Property<Guid>(role, "Id") == second)
            .Select(role => role.IsDeveloper).SingleAsync());
        Assert.Equal(first, (Guid)Assert.Single(await context.Set<Dictionary<string, object>>("UserRoles").ToListAsync())["RoleId"]);
        Assert.Equal(first, (Guid)Assert.Single(await context.Set<Dictionary<string, object>>("RoleGrants").ToListAsync())["RoleId"]);
        Assert.Equal(second, (Guid)Assert.Single(await context.Set<Dictionary<string, object>>("MenuItemRoles").ToListAsync())["RoleId"]);
    }
}
