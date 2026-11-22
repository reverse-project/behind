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

public sealed class SubaccountMigrationTests
{
    [Fact]
    public async Task SqliteUpgradePreservesUsersAndEnforcesFamilyDepth()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await VerifyFamilySchema(db, true);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgresEnforcesFamilyDepth(bool upgrade)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        await using var db = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options);
        await VerifyFamilySchema(db, upgrade);
    }

    private static async Task VerifyFamilySchema(AbstractCoreContext db, bool upgrade)
    {
        var primary = Guid.NewGuid();
        if (upgrade)
        {
            await db.GetService<IMigrator>().MigrateAsync("20261118160000_PreserveIssuedBans");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Users\" (\"Id\", \"DisplayName\") VALUES ({primary}, {"Primary"})");
        }

        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        if (!upgrade)
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Users\" (\"Id\", \"DisplayName\") VALUES ({primary}, {"Primary"})");
        }

        var child = new HumanUser { DisplayName = "Child", Birthday = new DateOnly(2012, 4, 12) };
        var other = new HumanUser { DisplayName = "Other" };
        db.Users.AddRange(child, other);
        await db.SaveChangesAsync();
        var childId = db.GetKey(child);
        var otherId = db.GetKey(other);
        db.Subaccounts.Add(new Subaccount(primary, childId, true, null));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = (await db.GetRepository<HumanUser, Guid>().TryFindAsync(childId)).GetOrElse(new HumanUser());
        Assert.Equal(child.Birthday, loaded.Birthday);
        Assert.Empty(loaded.Emails);
        Assert.Equal(childId, await db.GetUserKeyAsync(loaded));
        var labels = await db.ResolveAuditLabelsAsync([childId], []);
        Assert.Contains("Child", labels.Label(childId.ToString()));
        foreach (var invalid in new[]
                 {
                     new Subaccount(childId, otherId, false, null),
                     new Subaccount(otherId, primary, false, null),
                     new Subaccount(otherId, otherId, false, null),
                     new Subaccount(otherId, childId, false, null)
                 })
        {
            db.Subaccounts.Add(invalid);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
        }

        await db.Database.MigrateAsync();
        Assert.Equal(primary, (await db.Subaccounts.SingleAsync()).PrimaryUserId);
    }
}
