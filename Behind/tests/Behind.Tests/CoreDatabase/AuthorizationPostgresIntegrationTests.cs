using Behind.Core.Database;
using Behind.Core.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class AuthorizationPostgresIntegrationTests
{
    [Fact]
    public async Task DeletedVisibilityNeverRestoresRetiredRoleGrants()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        await using var context = new CoreContextPostgres(
            new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options);
        await context.Database.MigrateAsync();
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);
        var user = new HumanUser("Test", [new Email("security@example.test")], None);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var userId = context.Entry(user).Property<Guid>("Id").CurrentValue;
        await context.Database.ExecuteSqlAsync($"INSERT INTO \"UserRoles\" (\"UserId\", \"RoleId\") VALUES ({userId}, {BuiltInAccess.Administrators.Id})");
        Assert.True((await context.GetUserAccessAsync(userId)).Holds("User.Manage"));
        var role = await context.Roles.SingleAsync(item => EF.Property<Guid>(item, "Id") == BuiltInAccess.Administrators.Id);
        context.Remove(role);
        await context.SaveChangesAsync();
        using var scope = context.SoftDelete.IncludeDeleted();
        Assert.False((await context.GetUserAccessAsync(userId)).Holds("User.Manage"));
    }
}
