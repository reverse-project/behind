using Behind.Core.Database.Postgres;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class OidcRoleNamesPostgresIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoleNamesAreCaseSensitiveOnFreshAndUpgradedDatabases(bool upgrade)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image must be configured.");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var context = new CoreContextPostgres(options);
        Guid? existingRole = null;
        if (upgrade)
        {
            await context.GetService<IMigrator>().MigrateAsync("20261116103140_PersistOidcAccounts");
            existingRole = Guid.NewGuid();
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO "Roles" ("Id", "Name", "IsDeveloper")
                VALUES ({existingRole}, {"Legacy:équipe"}, {false}),
                    ({Guid.NewGuid()}, {"Legacy:s"}, {false}),
                    ({Guid.NewGuid()}, {"Legacy:ſ"}, {false})
                """);
        }

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        var columns = await context.Database.SqlQueryRaw<string>("""
            SELECT column_name AS "Value" FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'Roles'
            ORDER BY column_name
            """).ToListAsync();
        Assert.Equal(["Id", "IsDeveloper", "Name"], columns);
        if (existingRole is not null)
        {
            Assert.Equal(3, await context.Roles.CountAsync());
            Assert.Contains(await context.Roles.Select(role => role.Name).ToListAsync(), name => name == "Legacy:équipe");
            context.Roles.Add(new UserRole("LEGACY:ÉQUIPE"));
            await context.SaveChangesAsync();
        }

        var userId = await SeedUser(context, "role@test.example");
        var store = new Behind.Core.Database.OidcAccountStore(context);
        await store.SynchronizeRoles(userId, "provider", [], ["oidc:Readers"], CancellationToken.None);
        await store.SynchronizeRoles(userId, "provider", [], ["OIDC:READERS"], CancellationToken.None);
        Assert.Single(await context.Roles.Where(role => role.Name == "oidc:Readers").ToListAsync());
        Assert.Single(await context.Roles.Where(role => role.Name == "OIDC:READERS").ToListAsync());
        var secondUserId = await SeedUser(context, "second-role@test.example");
        await using (var first = new CoreContextPostgres(options))
        await using (var second = new CoreContextPostgres(options))
        {
            await Task.WhenAll(
                new Behind.Core.Database.OidcAccountStore(first).SynchronizeRoles(
                    userId, "concurrent", [], ["oidc:équipe"], CancellationToken.None),
                new Behind.Core.Database.OidcAccountStore(second).SynchronizeRoles(
                    secondUserId, "concurrent", [], ["oidc:équipe"], CancellationToken.None));
        }

        var concurrentRole = (await context.Roles.ToListAsync()).Single(role =>
            string.Equals(role.Name, "oidc:équipe", StringComparison.Ordinal));
        var concurrentRoleId = context.Entry(concurrentRole).Property<Guid>("Id").CurrentValue;
        Assert.Equal(2, await context.Set<Dictionary<string, object>>("UserRoles")
            .CountAsync(row => EF.Property<Guid>(row, "RoleId") == concurrentRoleId));
        await store.SynchronizeRoles(userId, "provider-b", ["oidc:Readers"], [], CancellationToken.None);
        await using (var first = new CoreContextPostgres(options))
        await using (var second = new CoreContextPostgres(options))
        {
            await Task.WhenAll(
                new Behind.Core.Database.OidcAccountStore(first).SynchronizeRoles(
                    userId, "provider", [], [], CancellationToken.None),
                new Behind.Core.Database.OidcAccountStore(second).SynchronizeRoles(
                    userId, "provider-b", [], [], CancellationToken.None));
        }

        Assert.Equal(2, await context.Set<Dictionary<string, object>>("UserRoles").CountAsync());
        foreach (var constraint in new[] { "IX_Roles_Name", "UnrelatedConstraint" })
        {
            var interceptor = new UniqueViolationInterceptor(constraint);
            await using var retryContext = new CoreContextPostgres(
                new DbContextOptionsBuilder<CoreContextPostgres>()
                    .UseNpgsql(postgres.GetConnectionString()).AddInterceptors(interceptor).Options);
            var synchronize = new Behind.Core.Database.OidcAccountStore(retryContext).SynchronizeRoles(
                userId, "provider-retry", [], [$"oidc:{constraint}"], CancellationToken.None);
            if (constraint == "IX_Roles_Name")
            {
                await synchronize;
                Assert.Equal(2, interceptor.Attempts);
            }
            else
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => synchronize);
                Assert.Equal(1, interceptor.Attempts);
            }
        }

        await RoleNameUnicodeTests.AssertCaseSensitiveUniqueness(context);
        var accessCommands = new Behind.Core.Database.Commands.AccessSettingsCommands(
            context, Substitute.For<Behind.Core.Database.IVfsCommands>());
        var draft = new Behind.Core.Database.IAccessSettingsCommands.AccessSettingsDraft(true, "Équipe", "", false);
        Assert.IsType<Behind.Core.Database.AccessSettingsSaveResult.Saved>(await accessCommands.Save(draft));
        Assert.IsType<Behind.Core.Database.AccessSettingsSaveResult.UnknownRole>(
            await accessCommands.Save(draft with { RestrictedToRole = "éQUiPE" }));

        var commandInterceptor = new UniqueViolationInterceptor("IX_Roles_Name");
        await using (var commandContext = new CoreContextPostgres(
                         new DbContextOptionsBuilder<CoreContextPostgres>()
                             .UseNpgsql(postgres.GetConnectionString()).AddInterceptors(commandInterceptor).Options))
        {
            var concurrentCreate = new Behind.Core.Database.Commands.RoleCommands(commandContext, AccessCatalog.Core);
            Assert.True((await concurrentCreate.Create("conflicting-insert")).IsNone());
            Assert.Empty(commandContext.ChangeTracker.Entries<UserRole>());
        }

        context.Roles.Add(new UserRole("OIDC:READERS"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal("IX_Roles_Name", Assert.IsType<PostgresException>(exception.InnerException).ConstraintName);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static async Task<Guid> SeedUser(CoreContextPostgres context, string email)
    {
        var user = new HumanUser("OIDC test", [new Email(email)], None);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return context.Entry(user).Property<Guid>("Id").CurrentValue;
    }

    private sealed class UniqueViolationInterceptor(string constraint) : SaveChangesInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<UserRole>().Any(entry => entry.State == EntityState.Added))
            {
                Attempts++;
                if (Attempts == 1)
                {
                    throw new DbUpdateException("Concurrent insert", new PostgresException(
                        "Concurrent insert", "ERROR", "ERROR", "23505", constraintName: constraint));
                }
            }

            return ValueTask.FromResult(result);
        }
    }
}
