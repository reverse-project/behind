using Behind.Common.Database.Modules;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Forum.Database;
using Behind.Forum.Database.Commands;
using Behind.Forum.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Behind.Tests.Forum;

public sealed class ContentRestrictionModuleTests : IDisposable
{
    private readonly SportTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task ItemBrowser_ResolvesModuleNamesAndPostBodies()
    {
        await using var context = _bed.Write();
        var board = Assert.IsType<VersionedSaveResult.Saved>(await new ForumBoardCommands(context)
            .Create(new ForumBoard("Browsable board", "", null))).Id.Key;
        var commands = new ForumPostCommands(context);
        var topic = Assert.IsType<VersionedSaveResult.Saved>(await commands
            .Create(new ForumPost(board, null, Guid.NewGuid(), "Topic", "Body", PublicationState.Published))).Id.Key;
        var post = Assert.IsType<VersionedSaveResult.Saved>(await commands
            .Create(new ForumPost(board, topic, Guid.NewGuid(), "", "Message label", PublicationState.Published))).Id.Key;

        var boards = await context.BrowseContentItems(typeof(ForumBoard), new Flipper(1, 20), "Browsable");
        Assert.Equal(board, Assert.Single(boards.Items).Id);
        Assert.Equal("Browsable board", Assert.Single(boards.Items).DisplayName);
        var posts = await context.BrowseContentItems(typeof(ForumPost), new Flipper(1, 20), post.ToString());
        Assert.Equal("Message label", Assert.Single(posts.Items).DisplayName);
        foreach (var type in context.RestrictedContentTypes)
        {
            await context.BrowseContentItems(type, new Flipper(1, 20), null);
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task ModuleRestrictions_InheritThroughCategoriesBoardsTopicsRepliesAndWidgets(bool enabled, int expected)
    {
        await using var context = _bed.Write();
        var category = Assert.IsType<VersionedSaveResult.Saved>(await new ForumBoardCategoryCommands(context)
            .Create(new ForumBoardCategory("Adults"))).Id.Key;
        var board = Assert.IsType<VersionedSaveResult.Saved>(await new ForumBoardCommands(context)
            .Create(new ForumBoard("Board", "Description", null, category))).Id.Key;
        var commands = new ForumPostCommands(context);
        var topic = Assert.IsType<VersionedSaveResult.Saved>(await commands
            .Create(new ForumPost(board, null, Guid.NewGuid(), "Restricted", "Body", PublicationState.Published))).Id.Key;
        var reply = Assert.IsType<VersionedSaveResult.Saved>(await commands
            .Create(new ForumPost(board, topic, Guid.NewGuid(), "", "Reply", PublicationState.Published))).Id.Key;
        var restriction = new ContentRestriction(nameof(ForumBoardCategory), category, 18);
        context.ContentRestrictions.Add(restriction);
        context.Entry(restriction).StampVersionedKey(Guid.NewGuid(), 1);
        await context.SaveChangesAsync();
        var configuration = Substitute.For<IOptionsMonitor<ContentRestrictionsConfiguration>>();
        configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration { Enabled = enabled });

        await new ContentRestrictionMiddleware(_ => Task.CompletedTask).InvokeAsync(new DefaultHttpContext(), context,
            Substitute.For<ISessionQueries>(), new FixedTimeProvider(SportTestBed.Now), configuration);

        var queries = new ForumQueries(context);
        Assert.Equal(expected, (await queries.Boards(new Flipper(1, 10), DeletionState.Active, default)).Total);
        Assert.Equal(expected, (await queries.Posts(
            new Flipper(1, 10), board, null, false, null, DeletionState.Active, default)).Total);
        Assert.Equal(expected, (await queries.Posts(
            new Flipper(1, 10), board, topic, false, null, DeletionState.Active, default)).Total);
        Assert.Equal(!enabled, await queries.Post(reply, default) is not null);
        Assert.Equal(enabled ? 0 : 2, (await queries.LatestPosts(
            new ModuleCreationOrder(context), 10, default)).Total);
        Assert.Equal(expected, await context.ForumBoardCategories.CountAsync());
    }

    [Fact]
    public void ModuleDescriptors_RegisterTheirContentWithoutCoreDependencies()
    {
        using var context = _bed.Read();
        var types = context.RestrictedContentTypes;
        Assert.Contains(typeof(ForumPost), types);
        Assert.Contains(typeof(ForumBoard), types);
        Assert.Contains(typeof(ForumBoardCategory), types);
        Assert.Contains(typeof(Behind.Sport.Domain.Player), types);
        Assert.Contains(typeof(Behind.Sport.Domain.Stat), types);
        Assert.Contains(typeof(Behind.Quotes.Domain.Quote), types);
        Assert.DoesNotContain(typeof(ForumUserModeration), types);
        Assert.DoesNotContain(typeof(HumanUser), types);
    }
}
