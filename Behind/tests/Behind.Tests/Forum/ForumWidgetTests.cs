using Behind.Core.Database;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Behind.Forum.Database.Commands;
using Behind.Forum.Domain;

namespace Behind.Tests.Forum;

public sealed class ForumWidgetTests : IDisposable
{
    private readonly SportTestBed _bed = new();

    [Fact]
    public async Task Latest_OnlyIncludesPublishedPostsInActiveBoardsAndTopics()
    {
        await using var context = _bed.Write();
        var board = Assert.IsType<VersionedSaveResult.Saved>(await new ForumBoardCommands(context)
            .Create(new ForumBoard("Board", "", null))).Id.Key;
        var commands = new ForumPostCommands(context);
        var author = Guid.NewGuid();
        var topic = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ForumPost(board, null, author, "Topic", "Message", PublicationState.Published))).Id.Key;
        var reply = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ForumPost(board, topic, author, "", "Reply", PublicationState.Published))).Id.Key;
        await commands.Create(new ForumPost(board, null, author, "Pending", "Message", PublicationState.Pending));
        var queries = new ForumQueries(context);
        var ordering = new ModuleCreationOrder(context);

        var page = await queries.LatestPosts(ordering, 50, default);
        Assert.Equal(2, page.Total);
        Assert.Contains(page.Items, post => post.Key == topic);
        Assert.Contains(page.Items, post => post.Key == reply);
        Assert.Single((await queries.LatestPosts(ordering, 1, default)).Items);

        var original = (await queries.Post(topic, default))!;
        await commands.Update(topic, original.Entity with { State = PublicationState.Rejected });
        Assert.Empty((await queries.LatestPosts(ordering, 50, default)).Items);
    }

    public void Dispose() => _bed.Dispose();
}
