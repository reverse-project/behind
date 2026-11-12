using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class AnonymousCommentTests
{
    [Fact]
    public void LegacyEditRequests_LeaveAnonymityUnspecified()
    {
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<NewsCommentEndpoints.SaveNewsCommentRequest>(
            """{"Content":"Edited"}""")!.IsAnonymous);
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<ChatEndpoints.EditRequest>(
            """{"Text":"Edited","Version":1}""")!.IsAnonymous);
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<Behind.Forum.WebApi.Endpoints.ForumEndpoints.PostRequest>(
            """{"Body":"Edited"}""")!.IsAnonymous);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Response_HidesAnonymousIdentityEvenFromOwner(bool owner, bool moderator)
    {
        var author = Guid.NewGuid();
        var listing = new INewsCommentQueries.NewsCommentListing(
            new VersionedKey<Guid>(Guid.NewGuid()), author, "Real author", "Message", None,
            IsAnonymous: true);

        var item = listing.ToItem(owner ? Some(author) : None, moderator, false);

        Assert.True(item.IsAnonymous);
        Assert.Equal(moderator ? author : (Guid?)null, item.AuthorId);
        Assert.Equal(moderator ? "Real author" : "", item.Author);
        Assert.Equal(owner, item.CanEdit);
        Assert.Equal(owner || moderator, item.CanDelete);
    }

    [Fact]
    public async Task Save_PersistsAnonymityAndRealAuthorAcrossVersions()
    {
        using var bed = new NewsTestBed();
        var news = await bed.CreateNewsAsync();
        Guid id;
        await using (var context = bed.Write())
        {
            var result = await new NewsCommentCommands(context, new FixedTimeProvider(NewsTestBed.Now))
                .Add(news, bed.AuthorId, "Hidden", isAnonymous: true);
            id = result.GetOrElse(Guid.Empty);
            Assert.NotEqual(Guid.Empty, id);
        }

        await using (var context = bed.Read())
        {
            var queries = new NewsCommentQueries(context);
            var page = await queries.List(news, new Flipper(1, 10));
            var row = Assert.Single(page.Results.GetOrElse([]));
            Assert.True(row.IsAnonymous);
            Assert.Equal(bed.AuthorId, row.AuthorId);
            Assert.Equal(bed.AuthorId, (await queries.FindAuthor(id)).GetOrElse(Guid.Empty));
        }

        await using (var context = bed.Write())
        {
            Assert.True(await new NewsCommentCommands(context, new FixedTimeProvider(NewsTestBed.Now))
                .Update(id, "Still hidden"));
        }

        await using (var context = bed.Read())
        {
            var row = Assert.Single((await new NewsCommentQueries(context).List(news, new Flipper(1, 10))).Results.GetOrElse([]));
            Assert.True(row.IsAnonymous);
            Assert.Null(row.ToItem(None, false, false).AuthorId);
            Assert.Equal(2, row.Id.Version);
        }

        await using (var context = bed.Write())
        {
            Assert.True(await new NewsCommentCommands(context, new FixedTimeProvider(NewsTestBed.Now))
                .Update(id, "Public", isAnonymous: false));
        }

        await using (var context = bed.Read())
        {
            var row = Assert.Single((await new NewsCommentQueries(context).List(news, new Flipper(1, 10))).Results.GetOrElse([]));
            Assert.False(row.IsAnonymous);
            Assert.Equal(bed.AuthorId, row.AuthorId);
            Assert.Equal(bed.AuthorId, row.ToItem(None, false, false).AuthorId);
            Assert.Equal(3, row.Id.Version);
        }
    }

    [Fact]
    public async Task AnonymousRequest_StillRequiresSignedInAuthor()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        var result = await NewsCommentEndpoints.Add(Guid.NewGuid(),
            new NewsCommentEndpoints.SaveNewsCommentRequest("Hidden", true), new DefaultHttpContext(),
            Substitute.For<ISessionQueries>(), commands);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs().Add(default, default, "", default, false);
    }
}
