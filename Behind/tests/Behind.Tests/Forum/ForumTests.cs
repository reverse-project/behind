using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Behind.Forum.Database.Commands;
using Behind.Forum.Domain;
using Behind.Forum.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Behind.Tests.Forum;

public sealed class ForumTests : IDisposable
{
    private readonly SportTestBed _bed = new();
    private readonly Guid _author = Guid.NewGuid();

    public void Dispose() => _bed.Dispose();

    private async Task<Guid> Board(ModerationMode? mode = null)
    {
        await using var context = _bed.Write();
        return Assert.IsType<VersionedSaveResult.Saved>(
            await new ForumBoardCommands(context).Create(new ForumBoard("General", "Discussion", mode))).Id.Key;
    }

    private async Task<Guid> Post(Guid board, Guid? topic = null, PublicationState state = PublicationState.Published)
    {
        await using var context = _bed.Write();
        return Assert.IsType<VersionedSaveResult.Saved>(
            await new ForumPostCommands(context).Create(new ForumPost(board, topic, _author, "Topic", "Message", state))).Id.Key;
    }

    private async Task<IReadOnlyList<Keyed<ForumPost>>> PublicPosts(Guid board, Guid? topic = null)
    {
        await using var context = _bed.Read();
        return (await new ForumQueries(context).Posts(new Flipper(1, 100), board, topic, false, null,
            DeletionState.Active, default)).Items;
    }

    private static IModuleUsers Users(Guid id)
    {
        var users = Substitute.For<IModuleUsers>();
        users.Find(default!, default).ReturnsForAnyArgs(new[] { new IModuleUsers.Profile(id, "Ada", null) });
        return users;
    }

    [Theory]
    [InlineData(ModerationMode.Post, false, false, null, null, PublicationState.Published)]
    [InlineData(ModerationMode.Pre, false, false, ModerationMode.Post, ModerationMode.Post, PublicationState.Pending)]
    [InlineData(ModerationMode.Post, true, false, ModerationMode.Pre, ModerationMode.Post, PublicationState.Pending)]
    [InlineData(ModerationMode.Post, false, true, ModerationMode.Post, ModerationMode.Pre, PublicationState.Pending)]
    [InlineData(ModerationMode.Pre, true, true, ModerationMode.Pre, ModerationMode.Post, PublicationState.Published)]
    [InlineData(ModerationMode.Post, true, true, null, null, PublicationState.Published)]
    public void Moderation_UsesEnabledUserThenBoardThenGlobal(
        ModerationMode global, bool boards, bool users, ModerationMode? board, ModerationMode? user, PublicationState expected)
    {
        var settings = new ForumSettings { Moderation = global, AllowBoardOverrides = boards, AllowUserOverrides = users };

        Assert.Equal(expected, settings.InitialState(board, user));
    }

    [Fact]
    public async Task PublicList_ExcludesPendingRejectedAndPreviousPublishedVersions()
    {
        var board = await Board();
        var published = await Post(board);
        await Post(board, state: PublicationState.Pending);
        await Post(board, state: PublicationState.Rejected);
        Assert.Equal(published, Assert.Single(await PublicPosts(board)).Key);

        await using (var context = _bed.Write())
        {
            var old = (await new ForumQueries(context).Post(published, default))!;
            var result = await new ForumPostCommands(context).Update(published, old.Entity with { State = PublicationState.Rejected });
            Assert.Equal(2, Assert.IsType<VersionedSaveResult.Saved>(result).Id.Version);
        }

        Assert.Empty(await PublicPosts(board));
    }

    [Fact]
    public async Task PendingPost_IsPublishedOnlyAfterReview()
    {
        var board = await Board();
        var post = await Post(board, state: PublicationState.Pending);
        Assert.Empty(await PublicPosts(board));
        await using (var context = _bed.Write())
        {
            var result = await ForumEndpoints.Review(post,
                new ForumEndpoints.ReviewRequest(PublicationState.Published),
                new DefaultHttpContext(), new ForumQueries(context), new ForumPostCommands(context));
            Assert.IsType<JsonHttpResult<VersionedSavedResponse>>(result);
        }

        Assert.Equal(post, Assert.Single(await PublicPosts(board)).Key);
    }

    [Fact]
    public async Task RejectedOrDeletedTopic_HidesAllReplies()
    {
        var board = await Board();
        var topic = await Post(board);
        await Post(board, topic);
        Assert.Single(await PublicPosts(board, topic));
        await using (var context = _bed.Write())
        {
            var old = (await new ForumQueries(context).Post(topic, default))!;
            await new ForumPostCommands(context).Update(topic, old.Entity with { State = PublicationState.Rejected });
        }

        Assert.Empty(await PublicPosts(board, topic));
        await using var read = _bed.Read();
        Assert.IsType<NotFound>(await ForumEndpoints.GetPost(topic, new DefaultHttpContext(),
            new ForumQueries(read), Users(_author)));
    }

    [Fact]
    public async Task Replies_RequireAnExistingPublishedTopicInTheSameBoard()
    {
        var board = await Board();
        var otherBoard = await Board();
        var pending = await Post(board, state: PublicationState.Pending);
        var otherTopic = await Post(otherBoard);
        await using var context = _bed.Write();
        var commands = new ForumPostCommands(context);

        foreach (var topic in new[] { pending, otherTopic, Guid.NewGuid() })
        {
            var result = await commands.Create(new ForumPost(board, topic, _author, "", "Reply", PublicationState.Published));
            Assert.Equal("unknownTopic", Assert.IsType<VersionedSaveResult.Rejected>(result).Reason);
        }
    }

    [Fact]
    public async Task BoardDeletion_HidesPosts_RestoreBringsThemBack_HardDeleteRefusesReferences()
    {
        var board = await Board();
        await Post(board);
        await using (var context = _bed.Write())
        {
            Assert.True(await new ForumBoardCommands(context).Delete(board));
        }

        Assert.Empty(await PublicPosts(board));
        await using (var context = _bed.Write())
        {
            var commands = new ForumBoardCommands(context);
            Assert.Equal(HardDeleteResult.Rejected, await commands.HardDelete(board));
            Assert.True(await commands.Restore(board));
        }

        Assert.Single(await PublicPosts(board));
    }

    [Fact]
    public async Task BoardCategories_CanBeAssignedAndCannotBeDeletedWhileReferenced()
    {
        Guid category;
        await using (var context = _bed.Write())
        {
            var created = Assert.IsType<VersionedSaveResult.Saved>(
                await new ForumBoardCategoryCommands(context).Create(new ForumBoardCategory("Announcements")));
            category = created.Id.Key;
            var duplicate = await new ForumBoardCategoryCommands(context)
                .Create(new ForumBoardCategory("Announcements"));
            Assert.Equal("nameTaken", Assert.IsType<VersionedSaveResult.Rejected>(duplicate).Reason);
            var invalidBoard = await new ForumBoardCommands(context)
                .Create(new ForumBoard("Invalid", "", null, Guid.NewGuid()));
            Assert.Equal("unknownCategory", Assert.IsType<VersionedSaveResult.Rejected>(invalidBoard).Reason);

            var board = await new ForumBoardCommands(context)
                .Create(new ForumBoard("News", "Latest news", null, category));
            Assert.IsType<VersionedSaveResult.Saved>(board);
            Assert.False(await new ForumBoardCategoryCommands(context).Delete(category));
            var deletion = await ForumEndpoints.DeleteCategory(
                category, new DefaultHttpContext(), new ForumQueries(context),
                new ForumBoardCategoryCommands(context));
            Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(deletion).StatusCode);
        }

        await using (var context = _bed.Read())
        {
            var queries = new ForumQueries(context);
            var categories = await queries.ActiveCategories(default);
            Assert.Equal(category, Assert.Single(categories).Key);
            var categoryResponse = Assert.IsType<Ok<ForumEndpoints.BoardCategoryResponse[]>>(
                await ForumEndpoints.Categories(queries, default));
            Assert.Equal(category, Assert.Single(categoryResponse.Value!).Key);
            var boards = await queries.Boards(new Flipper(1, 100), DeletionState.Active, default);
            Assert.Equal(category, Assert.Single(boards.Items).Entity.CategoryId);
            var source = new ForumAuditLabelSource(context, Users(_author));
            var labels = await context.ResolveAuditLabelsAsync([category], [source]);
            Assert.Equal("Announcements", labels.Label(category.ToString()));
        }

        await using (var context = _bed.Write())
        {
            var board = await new ForumQueries(context).Boards(new Flipper(1, 100), DeletionState.Active, default);
            var commands = new ForumBoardCommands(context);
            Assert.IsType<VersionedSaveResult.Saved>(
                await commands.Update(board.Items[0].Key, board.Items[0].Entity with { CategoryId = null }));
            var categoryCommands = new ForumBoardCategoryCommands(context);
            Assert.True(await categoryCommands.Delete(category));
            Assert.True(await categoryCommands.Restore(category));
            Assert.True(await categoryCommands.Delete(category));
            Assert.Equal(HardDeleteResult.Deleted, await categoryCommands.HardDelete(category));
        }
    }

    [Fact]
    public async Task Posts_SupportDeleteRestoreAndHardDelete_WithAuditLabels()
    {
        var board = await Board();
        var post = await Post(board);
        await using (var context = _bed.Write())
        {
            var commands = new ForumPostCommands(context);
            Assert.True(await commands.Delete(post));
        }

        Assert.Empty(await PublicPosts(board));
        await using (var context = _bed.Write())
        {
            var commands = new ForumPostCommands(context);
            Assert.True(await commands.Restore(post));
        }

        Assert.Single(await PublicPosts(board));
        await using (var context = _bed.Write())
        {
            var source = new ForumAuditLabelSource(context, Users(_author));
            var labels = await context.ResolveAuditLabelsAsync([board, post], [source]);
            Assert.Equal("General", labels.Label(board.ToString()));
            Assert.Equal("Topic", labels.Label(post.ToString()));
            var commands = new ForumPostCommands(context);
            await commands.Delete(post);
            Assert.Equal(HardDeleteResult.Deleted, await commands.HardDelete(post));
        }

        Assert.Empty(await PublicPosts(board));
    }

    [Fact]
    public async Task CreatePost_UsesSessionIdentityAndCurrentOverrideSettings()
    {
        var board = await Board(ModerationMode.Pre);
        await using var context = _bed.Write();
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(Arg.Any<HttpContext>()).Returns((Guid?)_author);
        var settings = Substitute.For<IOptionsMonitor<ForumSettings>>();
        settings.CurrentValue.Returns(new ForumSettings { AllowBoardOverrides = true });
        var http = new DefaultHttpContext();
        var result = await ForumEndpoints.CreatePost(
            new ForumEndpoints.PostRequest(board, null, "Title", "Body"), http, identity,
            new ForumQueries(context), new ForumPostCommands(context), settings);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var created = Assert.Single(await context.ForumPosts.ToKeyedAsync());
        Assert.Equal(_author, created.Entity.AuthorId);
        Assert.Equal(PublicationState.Pending, created.Entity.State);
        Assert.Empty(await PublicPosts(board));
    }

    [Fact]
    public async Task CreatePost_AnonymousCannotSubmit()
    {
        await using var context = _bed.Write();
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(Arg.Any<HttpContext>()).Returns((Guid?)null);
        Assert.IsType<UnauthorizedHttpResult>(await ForumEndpoints.CreatePost(
            new ForumEndpoints.PostRequest(Guid.NewGuid(), null, "Title", "Body"),
            new DefaultHttpContext(), identity, new ForumQueries(context),
            new ForumPostCommands(context), Substitute.For<IOptionsMonitor<ForumSettings>>()));
    }

    [Fact]
    public async Task UserRules_AreVersionedAndDisabledOverridesHaveNoEffect()
    {
        await using var context = _bed.Write();
        var commands = new ForumUserModerationCommands(context, Users(_author));
        Assert.True(await commands.Ensure(_author, new ForumUserModeration(_author, ModerationMode.Pre)));
        var queries = new ForumQueries(context);
        Assert.Equal(ModerationMode.Pre, await queries.UserMode(_author, default));
        Assert.Equal(PublicationState.Published, new ForumSettings().InitialState(null, await queries.UserMode(_author, default)));
        Assert.True(await commands.Delete(_author));
        Assert.Null(await queries.UserMode(_author, default));
        Assert.True(await commands.Restore(_author));
        Assert.Equal(ModerationMode.Pre, await queries.UserMode(_author, default));
    }

    [Fact]
    public async Task DeletedListing_RequiresDeveloperPermission()
    {
        await using var context = _bed.Read();
        var result = await ForumEndpoints.ManagePosts(new DefaultHttpContext(), new ForumQueries(context),
            Users(_author), new FakeAuthorizationService(), null, null, true);

        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task OverrideEditors_RejectOverridesUntilTheirFeaturesAreEnabled()
    {
        await using var context = _bed.Write();
        var settings = Substitute.For<IOptionsMonitor<ForumSettings>>();
        settings.CurrentValue.Returns(new ForumSettings());
        var board = await ForumEndpoints.SaveBoard(
            new ForumEndpoints.BoardRequest("Board", "", ModerationMode.Pre),
            new DefaultHttpContext(), new ForumBoardCommands(context), settings);
        var user = await ForumEndpoints.SaveRule(
            new ForumEndpoints.UserRuleRequest(_author, ModerationMode.Pre),
            new DefaultHttpContext(), new ForumUserModerationCommands(context, Users(_author)), settings);

        Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(board).StatusCode);
        Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(user).StatusCode);
        Assert.Empty(await context.ForumBoards.ToListAsync());
        Assert.Empty(await context.ForumUserModerations.ToListAsync());
    }

    [Fact]
    public async Task PublicAuthorProfiles_IncludePicturesWithoutSeparateEmailFields()
    {
        var board = await Board();
        var topic = await Post(board);
        await using var context = _bed.Read();
        var users = Substitute.For<IModuleUsers>();
        var picture = Guid.NewGuid();
        users.Find(default!, default).ReturnsForAnyArgs(new[] { new IModuleUsers.Profile(_author, "Ada", picture) });
        var result = await ForumEndpoints.GetPost(topic, new DefaultHttpContext(), new ForumQueries(context), users);
        var response = Assert.IsType<Ok<ForumEndpoints.PostResponse>>(result).Value!;

        Assert.Equal("Ada", response.Author);
        Assert.Equal($"/profile/images/{picture}", response.PictureUrl);
        Assert.DoesNotContain("email", System.Text.Json.JsonSerializer.Serialize(response), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Ada Lovelace", "Ada Lovelace")]
    [InlineData("  Ada Lovelace  ", "Ada Lovelace")]
    [InlineData("", "primary@example.com")]
    [InlineData("   ", "primary@example.com")]
    public async Task TopicAndReplyLists_ResolveStoredNamesWithPrimaryEmailFallback(string name, string expected)
    {
        var board = await Board();
        await using (var context = _bed.Write())
        {
            var user = new HumanUser(name, [new Email("primary@example.com"), new Email("secondary@example.com")], None);
            await context.Users.AddAsync(user);
            context.Entry(user).Property<Guid>("Id").CurrentValue = _author;
            context.Entry(user.Emails.First()).Property<Guid>("Id").CurrentValue =
                Guid.Parse("00000000-0000-0000-0000-000000000001");
            context.Entry(user.Emails.Last()).Property<Guid>("Id").CurrentValue =
                Guid.Parse("00000000-0000-0000-0000-000000000002");
            await context.SaveChangesAsync();
        }

        var topic = await Post(board);
        await Post(board, topic);
        await using var read = _bed.Read();
        var profiles = new UserProfiles(read, _bed.Files);
        foreach (var topicId in new Guid?[] { null, topic })
        {
            var result = await ForumEndpoints.Posts(new DefaultHttpContext(), new ForumQueries(read),
                profiles, board, topicId, 1);
            var response = Assert.IsType<Ok<ForumEndpoints.PostPage>>(result).Value!;
            var post = Assert.Single(response.Items);
            Assert.Equal(expected, post.Author);
            Assert.Equal(_author, post.AuthorId);
            Assert.DoesNotContain("secondary@example.com", System.Text.Json.JsonSerializer.Serialize(post));
        }

        var details = Assert.IsType<Ok<ForumEndpoints.PostResponse>>(
            await ForumEndpoints.GetPost(topic, new DefaultHttpContext(), new ForumQueries(read), profiles)).Value!;
        Assert.Equal(expected, details.Author);
    }
}
