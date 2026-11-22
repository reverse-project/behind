using System.Data.Common;
using Behind.Common.Database.Raw;
using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteSupportsSharedChildrenAndEnforcesFamilyDepth(bool upgrade)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoreContextSqlite(
            new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options);
        await VerifyFamilySchema(db, upgrade);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostgresSupportsSharedChildrenAndEnforcesFamilyDepth(bool upgrade)
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
        var legacyChild = Guid.NewGuid();
        var legacyNoPin = Guid.NewGuid();
        if (upgrade)
        {
            await db.GetService<IMigrator>().MigrateAsync("20261118160000_PreserveIssuedBans");
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Users\" (\"Id\", \"DisplayName\") VALUES ({primary}, {"Primary"})");
            await db.GetService<IMigrator>().MigrateAsync("20261122120000_AddSubaccounts");
            await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO "Users" ("Id", "DisplayName") VALUES ({legacyChild}, {"Legacy"}), ({legacyNoPin}, {"No PIN"})""");
            await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO "Subaccounts" ("UserId", "PrimaryUserId", "IsChild", "PinHash") VALUES ({legacyChild}, {primary}, {true}, {"legacy-hash"}), ({legacyNoPin}, {primary}, {true}, NULL)""");
            await db.GetService<IMigrator>().MigrateAsync("20261122140000_AddPendingFamilyLinks");
        }

        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        if (upgrade)
        {
            var credential = await db.UserSwitchPins.Include(pin => pin.User).SingleAsync();
            Assert.Equal(legacyChild, db.GetKey(credential.User));
            Assert.Equal(new PinHash("legacy-hash"), credential.PinHash);
            var preserved = await db.Subaccounts
                .Include(link => link.PrimaryUser)
                .Include(link => link.User)
                .ToListAsync();
            Assert.Equal(2, preserved.Count);
            Assert.All(preserved, link =>
            {
                Assert.True(link.IsChild);
                Assert.Equal(primary, db.GetKey(link.PrimaryUser));
            });
            Assert.Contains(preserved, link => db.GetKey(link.User) == legacyChild);
            Assert.Contains(preserved, link => db.GetKey(link.User) == legacyNoPin);
            await db.Database.ExecuteSqlAsync($"""DELETE FROM "Users" WHERE "Id" IN ({legacyChild}, {legacyNoPin})""");
            Assert.False(await db.UserSwitchPins.AnyAsync());
        }
        else
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Users\" (\"Id\", \"DisplayName\") VALUES ({primary}, {"Primary"})");
        }

        var requestId = Guid.NewGuid();
        var child = new HumanUser
        {
            DisplayName = "Child",
            Birthday = new DateOnly(2012, 4, 12),
            PendingParentId = primary,
            PendingLinkRequestId = requestId
        };
        var other = new HumanUser { DisplayName = "Other" };
        db.Users.AddRange(child, other);
        await db.SaveChangesAsync();
        var childId = db.GetKey(child);
        var otherId = db.GetKey(other);
        var primaryUser = await db.Users.FindAsync(primary) ?? throw new InvalidOperationException();
        var hash = new PinHash("hash-written-by-ef");
        db.Subaccounts.Add(new Subaccount(primaryUser, child, true));
        db.UserSwitchPins.Add(new UserSwitchPin(child, hash));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(hash.ToString(), await db.Database.SqlQuery<string>(
            $"""SELECT "PinHash" AS "Value" FROM "UserSwitchPins" WHERE "UserId" = {childId}""").SingleAsync());
        await db.Database.ExecuteSqlAsync(
            $"""UPDATE "UserSwitchPins" SET "PinHash" = {"legacy-hash"} WHERE "UserId" = {childId}""");
        var link = await db.Subaccounts
            .Include(account => account.PrimaryUser)
            .Include(account => account.User)
            .SingleAsync();
        Assert.Equal(primary, db.GetKey(link.PrimaryUser));
        Assert.Null(link.PrimaryUser.PendingParentId);
        Assert.Null(link.PrimaryUser.PendingLinkRequestId);
        Assert.Equal(primary, link.User.PendingParentId);
        Assert.Equal(requestId, link.User.PendingLinkRequestId);
        Assert.Equal(childId, db.GetKey(link.User));
        var pin = await db.UserSwitchPins.SingleAsync();
        Assert.Equal((PinHash?)"legacy-hash", pin.PinHash);
        Assert.Equal("legacy-hash", (string?)pin.PinHash);
        var raw = db.FindRawTable("UserSwitchPins").Match(() => throw new InvalidOperationException(), table => table);
        Assert.True(raw.FindColumn("PinHash").Match(() => false, column => column.IsSensitive));
        var page = (await new RawDatabaseQueries(db).ListRows(
                "UserSwitchPins", new RawRowQuery(new Flipper(1, 5), None, None, false)))
            .Match(() => throw new InvalidOperationException(), result => result);
        var rows = page.Results.Match(() => [], found => found.ToList());
        Assert.Equal(SensibleExtensions.MaskedValue, Assert.Single(rows).Values["PinHash"]);
        db.UserSwitchPins.Remove(pin);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.False(await db.UserSwitchPins.AnyAsync());
        db.ChangeTracker.Clear();

        var loaded = (await db.GetRepository<HumanUser, Guid>().TryFindAsync(childId)).GetOrElse(new HumanUser());
        Assert.Equal(child.Birthday, loaded.Birthday);
        Assert.Empty(loaded.Emails);
        Assert.Equal(childId, await db.GetUserKeyAsync(loaded));
        var labels = await db.ResolveAuditLabelsAsync([childId], []);
        Assert.Contains("Child", labels.Label(childId.ToString()));
        db.ChangeTracker.Clear();
        var secondParent = await db.Users.FindAsync(otherId) ?? throw new InvalidOperationException();
        var sharedChild = await db.Users.FindAsync(childId) ?? throw new InvalidOperationException();
        db.Subaccounts.Add(new Subaccount(secondParent, sharedChild, true));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        foreach (var (parentId, linkedId) in new[]
                 {
                     (childId, otherId),
                     (otherId, primary),
                     (otherId, otherId),
                     (primary, childId)
                 })
        {
            var parent = await db.Users.FindAsync(parentId) ?? throw new InvalidOperationException();
            var linked = await db.Users.FindAsync(linkedId) ?? throw new InvalidOperationException();
            db.Subaccounts.Add(new Subaccount(parent, linked, false));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
        }

        await db.Database.MigrateAsync();
        var owners = await db.Subaccounts
            .Where(link => EF.Property<Guid>(link, "UserId") == childId && link.IsChild)
            .Select(link => EF.Property<Guid>(link, "PrimaryUserId"))
            .ToListAsync();
        Assert.Equal(2, owners.Count);
        Assert.Contains(primary, owners);
        Assert.Contains(otherId, owners);
        await Assert.ThrowsAnyAsync<DbException>(() => db.Database.ExecuteSqlAsync(
            $"""DELETE FROM "Users" WHERE "Id" = {primary}"""));
        await Assert.ThrowsAnyAsync<DbException>(() => db.Database.ExecuteSqlAsync(
            $"""DELETE FROM "Users" WHERE "Id" = {otherId}"""));
        await db.Database.ExecuteSqlAsync($"""DELETE FROM "Users" WHERE "Id" = {childId}""");
        Assert.False(await db.Subaccounts.AnyAsync());
    }
}
