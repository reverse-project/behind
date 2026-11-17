using System.Globalization;
using System.Data.Common;
using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Behind.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class RoleNameUnicodeTests
{
    private static readonly (string First, string Second)[] Names =
    [
        ("équipe", "ÉQUIPE"),
        ("σ", "ς"),
        ("\U00010428", "\U00010400"),
        ("i", "İ"),
        ("s", "ſ"),
        ("k", "K"),
        ("é", "e\u0301"),
        ("ß", "SS")
    ];

    [Fact]
    public async Task SqliteRoleUniquenessAndRenamesUseOrdinalIgnoreCase()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        await AssertOrdinalUniqueness(context);
    }

    [Fact]
    public async Task SqliteUpgradeBackfillsUnicodeKeysAndPreservesOrdinallyDistinctNames()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261117054756_UniqueRoleNames");
        foreach (var name in new[] { "Legacy:équipe", "Legacy:ſ", "Legacy:s" })
        {
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO Roles (Id, Name, IsDeveloper) VALUES ({Guid.NewGuid()}, {name}, {false})");
        }

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        Assert.Equal(3, await context.Roles.CountAsync());
        context.Roles.Add(new UserRole("LEGACY:ÉQUIPE"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        await AssertOrdinalUniqueness(context);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task SqliteUpgradeRejectsExistingOrdinalCaseDuplicatesWithoutDiscardingRoles()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261117054756_UniqueRoleNames");
        foreach (var name in new[] { "équipe", "ÉQUIPE" })
        {
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO Roles (Id, Name, IsDeveloper) VALUES ({Guid.NewGuid()}, {name}, {false})");
        }

        await Assert.ThrowsAsync<SqliteException>(() => context.Database.MigrateAsync());
        Assert.Equal(new[] { "ÉQUIPE", "équipe" },
            (await context.Roles.Select(role => role.Name).ToListAsync()).Order(StringComparer.Ordinal));
    }

    internal static async Task AssertOrdinalUniqueness(AbstractCoreContext context)
    {
        using var culture = new CultureScope(CultureInfo.GetCultureInfo("tr-TR"));
        for (var index = 0; index < Names.Length; index++)
        {
            var (first, second) = Names[index];
            var equal = StringComparer.OrdinalIgnoreCase.Equals(first, second);
            var prefix = $"unicode-insert-{index}:";
            context.Roles.Add(new UserRole(prefix + first));
            await context.SaveChangesAsync();
            context.Roles.Add(new UserRole(prefix + second));
            if (equal)
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
                context.ChangeTracker.Clear();
            }
            else
            {
                await context.SaveChangesAsync();
            }

            prefix = $"unicode-raw-{index}:";
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsDeveloper\") VALUES ({Guid.NewGuid()}, {prefix + first}, {false})");
            if (equal)
            {
                await Assert.ThrowsAnyAsync<DbException>(() => context.Database.ExecuteSqlAsync(
                    $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsDeveloper\") VALUES ({Guid.NewGuid()}, {prefix + second}, {false})"));
            }
            else
            {
                await context.Database.ExecuteSqlAsync(
                    $"INSERT INTO \"Roles\" (\"Id\", \"Name\", \"IsDeveloper\") VALUES ({Guid.NewGuid()}, {prefix + second}, {false})");
            }

            prefix = $"unicode-rename-{index}:";
            context.Roles.Add(new UserRole(prefix + first));
            var renamed = new UserRole(prefix + "initial");
            context.Roles.Add(renamed);
            await context.SaveChangesAsync();
            context.Entry(renamed).Property(role => role.Name).CurrentValue = prefix + second;
            if (equal)
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
                context.ChangeTracker.Clear();
            }
            else
            {
                await context.SaveChangesAsync();
                Assert.Equal(prefix + second, await context.Roles.Where(role => role.Name == prefix + second)
                    .Select(role => role.Name).SingleAsync());
            }
        }
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
