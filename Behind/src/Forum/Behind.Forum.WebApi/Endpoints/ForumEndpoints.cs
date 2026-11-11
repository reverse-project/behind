using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Behind.Forum.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Behind.Forum.WebApi.Endpoints;

public static class ForumEndpoints
{
    public sealed record BoardRequest(
        [property: Required, MaxLength(200)] string Name,
        [property: MaxLength(2000)] string? Description,
        ModerationMode? Moderation,
        Guid? CategoryId = null);

    public sealed record BoardCategoryRequest([property: Required, MaxLength(200)] string Name);

    public sealed record PostRequest(
        Guid BoardId,
        Guid? TopicId,
        [property: MaxLength(200)] string? Title,
        [property: Required, MaxLength(20000)] string Body);

    public sealed record ReviewRequest(PublicationState State);

    public sealed record UserRuleRequest(Guid UserId, ModerationMode Moderation);

    public sealed record PostResponse(
        Guid Id, long Version, Guid BoardId, Guid? TopicId, Guid AuthorId,
        string Author, string? PictureUrl, string Title, string Body, PublicationState State);

    public sealed record PostPage(long Total, int Index, long PageCount, IReadOnlyList<PostResponse> Items);

    public sealed record BoardDetails(
        string Name, string Description, ModerationMode? Moderation, Guid? CategoryId);

    public sealed record BoardResponse(Guid Key, long Version, BoardDetails Entity);

    public sealed record BoardPage(long Total, int Index, long PageCount, IReadOnlyList<BoardResponse> Items);

    public sealed record BoardCategoryDetails(string Name);

    public sealed record BoardCategoryResponse(Guid Key, long Version, BoardCategoryDetails Entity);

    public sealed record BoardCategoryPage(long Total, int Index, long PageCount, IReadOnlyList<BoardCategoryResponse> Items);

    public sealed record PostSavedResponse(Guid Id, long Version, PublicationState State);

    [IgnoreETag]
    public static async Task<IResult> Boards(
        HttpContext http, ForumQueries queries, IAuthorizationService authorization,
        [FromQuery] int? page, [FromQuery] bool deleted = false) =>
        await http.WithDeletionState(authorization, deleted, async state =>
        {
            var boards = await queries.Boards(Paging(page), state, http.RequestAborted);

            return Results.Ok(new BoardPage(boards.Total, boards.Index, boards.PageCount,
                boards.Items.Select(board => new BoardResponse(board.Key, board.Version,
                    new BoardDetails(
                        board.Entity.Name, board.Entity.Description, board.Entity.Moderation, board.Entity.CategoryId)))
                    .ToArray()));
        });

    [IgnoreETag]
    public static async Task<IResult> Categories(ForumQueries queries, CancellationToken cancellationToken) =>
        Results.Ok((await queries.ActiveCategories(cancellationToken))
            .Select(category => new BoardCategoryResponse(
                category.Key, category.Version, new BoardCategoryDetails(category.Entity.Name)))
            .ToArray());

    [IgnoreETag]
    public static async Task<IResult> ManageCategories(
        HttpContext http, ForumQueries queries, IAuthorizationService authorization,
        [FromQuery] int? page, [FromQuery] bool deleted = false) =>
        await http.WithDeletionState(authorization, deleted, async state =>
        {
            var categories = await queries.Categories(Paging(page), state, http.RequestAborted);

            return Results.Ok(new BoardCategoryPage(
                categories.Total, categories.Index, categories.PageCount,
                categories.Items.Select(category => new BoardCategoryResponse(
                    category.Key, category.Version, new BoardCategoryDetails(category.Entity.Name)))
                    .ToArray()));
        });

    [IgnoreETag]
    public static async Task<IResult> Posts(
        HttpContext http, ForumQueries queries, IModuleUsers users,
        [FromQuery] Guid? boardId, [FromQuery] Guid? topicId, [FromQuery] int? page)
    {
        http.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        return Results.Ok(await ToPage(
            await queries.Posts(Paging(page), boardId, topicId, false, null, DeletionState.Active, http.RequestAborted),
            users, http.RequestAborted));
    }

    [IgnoreETag]
    public static async Task<IResult> LatestWidgetPosts(
        HttpContext http, ForumQueries queries, IModuleUsers users, IModuleCreationOrder ordering,
        [FromQuery] int? limit)
    {
        http.Response.Headers.CacheControl = ManagedListExtensions.NoStore;
        var page = await queries.LatestPosts(ordering, Math.Clamp(limit ?? 5, 1, 50), http.RequestAborted);

        return Results.Ok(await ToPage(page, users, http.RequestAborted));
    }

    [IgnoreETag]
    public static async Task<IResult> ManagePosts(
        HttpContext http, ForumQueries queries, IModuleUsers users, IAuthorizationService authorization,
        [FromQuery] int? page, [FromQuery] PublicationState? state, [FromQuery] bool deleted = false) =>
        await http.WithDeletionState(authorization, deleted, async deletion =>
            Results.Ok(await ToPage(
                await queries.Posts(Paging(page), null, null, true, state, deletion, http.RequestAborted),
                users, http.RequestAborted)));

    [IgnoreETag]
    public static async Task<IResult> CreatePost(
        PostRequest request, HttpContext http, IModuleIdentity identity,
        ForumQueries queries, IVersionedCommands<ForumPost> commands, IOptionsMonitor<ForumSettings> settings)
    {
        if (await identity.UserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var board = await queries.Board(request.BoardId, http.RequestAborted);

        if (board is null)
        {
            return Results.NotFound();
        }

        var state = settings.CurrentValue.InitialState(
            board.Entity.Moderation, await queries.UserMode(userId, http.RequestAborted));
        var result = await commands.Create(
            new ForumPost(request.BoardId, request.TopicId, userId,
                request.TopicId.HasValue ? "" : request.Title ?? "", request.Body, state),
            http.RequestAborted);

        return result is VersionedSaveResult.Saved saved
            ? Results.Created($"/forum/posts/{saved.Id.Key}", new PostSavedResponse(saved.Id.Key, saved.Id.Version, state))
            : result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> GetPost(Guid id, HttpContext http, ForumQueries queries, IModuleUsers users)
    {
        http.Response.Headers.CacheControl = ManagedListExtensions.NoStore;
        var post = await queries.Post(id, http.RequestAborted);

        if (post is null || post.Entity.State != PublicationState.Published
            || await queries.Board(post.Entity.BoardId, http.RequestAborted) is null)
        {
            return Results.NotFound();
        }

        if (post.Entity.TopicId is { } topicId)
        {
            var topic = await queries.Post(topicId, http.RequestAborted);

            if (topic is null || topic.Entity.State != PublicationState.Published)
            {
                return Results.NotFound();
            }
        }

        var page = await ToPage(new ForumQueries.Page<ForumPost>(1, 1, 1, [post]), users, http.RequestAborted);

        return Results.Ok(page.Items[0]);
    }

    [IgnoreETag]
    public static async Task<IResult> Review(
        Guid id, ReviewRequest request, HttpContext http, ForumQueries queries, IVersionedCommands<ForumPost> commands)
    {
        if (!Enum.IsDefined(request.State))
        {
            return Results.BadRequest();
        }

        var post = await queries.Post(id, http.RequestAborted);

        return post is null ? Results.NotFound()
            : (await commands.Update(id, post.Entity with { State = request.State }, http.RequestAborted)).ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> SaveBoard(
        BoardRequest request, HttpContext http, IVersionedCommands<ForumBoard> commands,
        IOptionsMonitor<ForumSettings> settings, Guid? id = null)
    {
        if (request.Moderation is { } mode && !Enum.IsDefined(mode))
        {
            return Results.BadRequest();
        }

        if (request.Moderation.HasValue && !settings.CurrentValue.AllowBoardOverrides)
        {
            return Results.Conflict(new { Reason = "boardOverridesDisabled" });
        }

        var draft = new ForumBoard(request.Name, request.Description ?? "", request.Moderation, request.CategoryId);
        var result = id.HasValue
            ? await commands.Update(id.Value, draft, http.RequestAborted)
            : await commands.Create(draft, http.RequestAborted);

        return result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> SaveCategory(
        BoardCategoryRequest request, HttpContext http, IVersionedCommands<ForumBoardCategory> commands, Guid? id = null)
    {
        var category = new ForumBoardCategory(request.Name);
        var result = id.HasValue
            ? await commands.Update(id.Value, category, http.RequestAborted)
            : await commands.Create(category, http.RequestAborted);

        return result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> DeleteCategory(
        Guid id, HttpContext http, ForumQueries queries, IVersionedCommands<ForumBoardCategory> commands)
    {
        if (await queries.CategoryIsAssigned(id, http.RequestAborted))
        {
            return Results.Conflict(new { Reason = "categoryInUse" });
        }

        return (await commands.Delete(id, http.RequestAborted)).ToNoContentResult();
    }

    [IgnoreETag]
    public static async Task<IResult> Rules(
        HttpContext http, ForumQueries queries, IModuleUsers users, IAuthorizationService authorization,
        [FromQuery] int? page, [FromQuery] bool deleted = false) =>
        await http.WithDeletionState(authorization, deleted, async state =>
        {
            var rules = await queries.UserRules(Paging(page), state, http.RequestAborted);
            var profiles = await users.Find(rules.Items.Select(rule => rule.Entity.UserId).ToArray(), http.RequestAborted);

            return Results.Ok(new { rules.Total, rules.Index, rules.PageCount,
                Items = rules.Items.Select(rule => new
                {
                    Id = rule.Key, rule.Version, rule.Entity.UserId, rule.Entity.Moderation,
                    Name = profiles.FirstOrDefault(profile => profile.Id == rule.Entity.UserId)?.DisplayName ?? ""
                }) });
        });

    [IgnoreETag]
    public static async Task<IResult> SaveRule(
        UserRuleRequest request, HttpContext http, IVersionedCommands<ForumUserModeration> commands,
        IOptionsMonitor<ForumSettings> settings)
    {
        if (!settings.CurrentValue.AllowUserOverrides)
        {
            return Results.Conflict(new { Reason = "userOverridesDisabled" });
        }

        if (!Enum.IsDefined(request.Moderation) || request.UserId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        var draft = new ForumUserModeration(request.UserId, request.Moderation);

        if (await commands.Ensure(request.UserId, draft, http.RequestAborted))
        {
            return Results.Ok();
        }

        return (await commands.Update(request.UserId, draft, http.RequestAborted)).ToResult();
    }

    public static IResult Settings(IOptionsMonitor<ForumSettings> settings) => Results.Ok(settings.CurrentValue);

    private static Flipper Paging(int? page) => Flipper.Create(page, 20, 20, 100);

    private static async Task<PostPage> ToPage(
        ForumQueries.Page<ForumPost> page, IModuleUsers users, CancellationToken cancellationToken)
    {
        var profiles = await users.Find(page.Items.Select(post => post.Entity.AuthorId).Distinct().ToArray(), cancellationToken);

        return new PostPage(page.Total, page.Index, page.PageCount, page.Items.Select(row =>
        {
            var post = row.Entity;
            var profile = profiles.FirstOrDefault(user => user.Id == post.AuthorId);

            return new PostResponse(row.Key, row.Version, post.BoardId, post.TopicId, post.AuthorId,
                profile?.DisplayName ?? "", profile?.PictureId is { } picture ? $"/profile/images/{picture}" : null,
                post.Title, post.Body, post.State);
        }).ToArray());
    }
}
