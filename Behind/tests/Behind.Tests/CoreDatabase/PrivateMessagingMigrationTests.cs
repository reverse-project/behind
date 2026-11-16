using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Behind.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class PrivateMessagingMigrationTests
{
    [Fact]
    public async Task UpgradePreservesAnnouncementsAndSupportsVersionedPrivateState()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(connection).Options;
        await using var context = new CoreContextSqlite(options);
        await context.GetService<IMigrator>().MigrateAsync("20261112115426_AddMessages");
        var sender = await DatabaseSeedHelper.SeedHumanUserAsync(context, "sender@example.test");
        var recipient = await DatabaseSeedHelper.SeedHumanUserAsync(context, "recipient@example.test");
        var announcementId = Guid.NewGuid();
        context.Add(new Message("Notice", "Maintenance", "<p>Existing content</p>", MessageLevel.Info, null, null))
            .StampVersionedKey(announcementId, 1);
        await context.SaveChangesAsync();
        await context.Database.MigrateAsync();
        Assert.Equal("Maintenance", await context.Messages.OfKey(announcementId).Select(message => message.Title).SingleAsync());
        var confirmed = new Email("notifications@example.test", DateTimeOffset.UtcNow);
        context.Add(confirmed).Property<Guid>("UserId").CurrentValue = recipient;
        await context.SaveChangesAsync();

        var store = new PrivateMessageStore(context);
        var result = await store.Create(sender, "Private", "Content", [recipient], [], [], [], false, false);
        Assert.True(result.Succeeded);
        var notification = Assert.Single(await new PrivateMessageEmailStore(context).Pending(default));
        Assert.Equal(recipient, notification.UserId);
        Assert.Equal("notifications@example.test", notification.Email);
        var conversation = (await store.Find(recipient, result.Id!.Value))!;
        var messageId = Assert.Single(conversation.Messages).Id;
        var readId = Guid.NewGuid();
        context.Add(new PrivateMessageRead(messageId, recipient)).StampVersionedKey(readId, 1);
        context.Add(new PrivateMessageRead(messageId, recipient)).StampVersionedKey(readId, 2);
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.PrivateMessageReads.OfKey(readId).CountAsync());
        Assert.Contains(context.Database.GetAppliedMigrations(), name => name.EndsWith("_AddPrivateMessagingAndReports", StringComparison.Ordinal));
        Assert.Contains(context.Database.GetAppliedMigrations(), name => name.EndsWith("_AddPrivateMessageEmails", StringComparison.Ordinal));
    }
}
