using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class PrivateMessageListingPostgresIntegrationTests
{
    [Fact]
    public async Task ListingKeepsVisibilityAndPaginationWithoutReadingMessageBodies()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Missing PostgreSQL image");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).LogTo(commands.Add).Options;
        await using var context = new CoreContextPostgres(options);
        await context.Database.MigrateAsync();
        var sender = await SeedUser(context, "sender@example.test");
        var recipient = await SeedUser(context, "recipient@example.test");
        var store = new PrivateMessageStore(context);
        for (var index = 0; index < 3; index++)
        {
            Assert.True((await store.Create(sender, $"subject {index}", "private body", [recipient], [], [], [], true, false)).Succeeded);
        }

        commands.Clear();
        var page = await store.List(recipient, "inbox", 2, 1);
        Assert.Equal(3, page.Total);
        Assert.Single(page.Items);
        Assert.DoesNotContain(commands, command => command.Contains(".\"Content\"", StringComparison.Ordinal));
    }
    private static async Task<Guid> SeedUser(CoreContextPostgres context, string address)
    {
        var user = new HumanUser(address, [new Email(address)], None);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return context.Entry(user).Property<Guid>("Id").CurrentValue;
    }

}
