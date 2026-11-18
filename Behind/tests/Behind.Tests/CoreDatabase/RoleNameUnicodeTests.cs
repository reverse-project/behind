using System.Data.Common;
using System.Globalization;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class RoleNameUnicodeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteRoleNamesAreCaseSensitiveOnFreshAndUpgradedDatabases(bool upgrade)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        if (upgrade)
        {
            await context.GetService<IMigrator>().MigrateAsync("20261116103131_PersistOidcAccounts");
            foreach (var name in new[] { "Legacy:équipe", "Legacy:ÉQUIPE", "Legacy:ſ", "Legacy:s" })
            {
                await context.Database.ExecuteSqlAsync(
                    $"INSERT INTO Roles (Id, Name, IsDeveloper) VALUES ({Guid.NewGuid()}, {name}, {false})");
            }
        }

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        Assert.Equal(upgrade ? 4 : 0, await context.Roles.CountAsync());
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_xinfo('Roles') ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.Equal(["Id", "IsDeveloper", "Name"], columns);
        await reader.DisposeAsync();
        await AssertCaseSensitiveUniqueness(context);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    internal static async Task AssertCaseSensitiveUniqueness(AbstractCoreContext context)
    {
        using var culture = new CultureScope(CultureInfo.GetCultureInfo("tr-TR"));
        var commands = new RoleCommands(context, AccessCatalog.Core);
        var first = await commands.Create("ÉQUIPE");
        var second = await commands.Create("équipe");
        Assert.False(first.IsNone());
        Assert.False(second.IsNone());
        Assert.True((await commands.Create("ÉQUIPE")).IsNone());
        var queries = new RoleQueries(context);
        Assert.Equal("ÉQUIPE", (await queries.GetRoleByName("ÉQUIPE")).Match(
            () => throw new InvalidOperationException(), role => role.Role.Name));
        Assert.Equal("équipe", (await queries.GetRoleByName("équipe")).Match(
            () => throw new InvalidOperationException(), role => role.Role.Name));
        Assert.True((await queries.GetRoleByName("Équipe")).IsNone());

        var renamed = new UserRole("rename-initial");
        context.Roles.Add(renamed);
        await context.SaveChangesAsync();
        context.Entry(renamed).Property(role => role.Name).CurrentValue = "Équipe";
        await context.SaveChangesAsync();
        context.Entry(renamed).Property(role => role.Name).CurrentValue = "ÉQUIPE";
        var conflict = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.True(RoleNameUniqueness.IsConflict(conflict));
        context.ChangeTracker.Clear();

        foreach (var name in new[] { "raw:équipe", "raw:ÉQUIPE", "raw:σ", "raw:ς", "raw:ſ", "raw:s" })
        {
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsDeveloper\") VALUES ({Guid.NewGuid()}, {name}, {false})");
        }

        await Assert.ThrowsAnyAsync<DbException>(() => context.Database.ExecuteSqlAsync(
            $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsDeveloper\") VALUES ({Guid.NewGuid()}, {"raw:ÉQUIPE"}, {false})"));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
        }

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
