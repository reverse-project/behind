using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Forum.Common;
using Behind.Forum.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Forum.WebApi.Endpoints;

public static class ForumRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapForumEndpoints()
        {
            var forum = application.MapGroup("/forum").WithTags("Forum");
            forum.MapGet("/boards", ForumEndpoints.Boards)
                .WithSummary("List forum boards; deleted records require developer permission.")
                .Produces<ForumEndpoints.BoardPage>();
            forum.MapGet("/manage/boards", ForumEndpoints.Boards)
                .RequireAuthorization(ForumNames.Manage)
                .Produces<ForumEndpoints.BoardPage>();
            forum.MapGet("/categories", ForumEndpoints.Categories)
                .WithSummary("List active forum board categories.")
                .Produces<IReadOnlyList<ForumEndpoints.BoardCategoryResponse>>();
            forum.MapGet("/manage/categories", ForumEndpoints.ManageCategories)
                .WithSummary("List forum board categories; deleted records require developer permission.")
                .Produces<ForumEndpoints.BoardCategoryPage>().RequireAuthorization(ForumNames.Manage);
            forum.MapGet("/posts", ForumEndpoints.Posts)
                .WithSummary("List published topics or replies in an active board.")
                .Produces<ForumEndpoints.PostPage>();
            forum.MapGet("/widgets/posts", ForumEndpoints.LatestWidgetPosts)
                .WithSummary("List the latest published forum posts.")
                .Produces<ForumEndpoints.PostPage>();
            forum.MapGet("/posts/{id:guid}", ForumEndpoints.GetPost)
                .WithSummary("Read a currently published forum message.")
                .Produces<ForumEndpoints.PostResponse>().Produces(404);
            forum.MapPost("/posts", ForumEndpoints.CreatePost)
                .WithSummary("Submit a topic or reply using the current moderation rules.")
                .Produces<ForumEndpoints.PostSavedResponse>(201).Produces(400).Produces(401).Produces(403).Produces(409)
                .RequireAuthorization(ForumNames.Read)
                .AddEndpointFilter<DataAnnotationValidationFilter<ForumEndpoints.PostRequest>>();
            forum.MapPost("/posts/{id:guid}", ForumEndpoints.UpdatePost)
                .WithSummary("Edit an owned forum message or moderate it.")
                .Produces<VersionedSavedResponse>().Produces(401).Produces(403).Produces(404)
                .RequireAuthorization(ForumNames.Read)
                .AddEndpointFilter<DataAnnotationValidationFilter<ForumEndpoints.PostRequest>>();
            forum.MapPost("/posts/{id:guid}/delete", ForumEndpoints.DeletePost)
                .WithSummary("Delete an owned forum message or moderate it.")
                .Produces(204).Produces(401).Produces(403).Produces(404).RequireAuthorization(ForumNames.Read);
            forum.MapGet("/settings", ForumEndpoints.Settings)
                .WithSummary("Read the current forum moderation settings.").Produces<ForumSettings>();
            forum.MapGet("/manage/posts", ForumEndpoints.ManagePosts)
                .WithSummary("List messages for moderator review.").Produces<ForumEndpoints.PostPage>()
                .RequireAuthorization(ForumNames.Moderate);
            forum.MapPost("/posts/{id:guid}/review", ForumEndpoints.Review)
                .WithSummary("Publish, reject or hold a forum message.")
                .Produces<VersionedSavedResponse>().Produces(404).Produces(409)
                .RequireAuthorization(ForumNames.Moderate);
            forum.MapPost("/boards", ForumEndpoints.SaveBoard).RequireAuthorization(ForumNames.Manage)
                .WithSummary("Create a forum board.").Produces<VersionedSavedResponse>()
                .AddEndpointFilter<DataAnnotationValidationFilter<ForumEndpoints.BoardRequest>>();
            forum.MapPost("/boards/{id:guid}", ForumEndpoints.SaveBoard).RequireAuthorization(ForumNames.Manage)
                .WithSummary("Save a new version of a forum board.").Produces<VersionedSavedResponse>()
                .AddEndpointFilter<DataAnnotationValidationFilter<ForumEndpoints.BoardRequest>>();
            forum.MapPost("/categories", ForumEndpoints.SaveCategory).RequireAuthorization(ForumNames.Manage)
                .WithSummary("Create a forum board category.").Produces<VersionedSavedResponse>()
                .AddEndpointFilter<DataAnnotationValidationFilter<ForumEndpoints.BoardCategoryRequest>>();
            forum.MapPost("/categories/{id:guid}", ForumEndpoints.SaveCategory).RequireAuthorization(ForumNames.Manage)
                .WithSummary("Save a new version of a forum board category.").Produces<VersionedSavedResponse>()
                .AddEndpointFilter<DataAnnotationValidationFilter<ForumEndpoints.BoardCategoryRequest>>();
            forum.MapPost("/categories/{id:guid}/delete", ForumEndpoints.DeleteCategory)
                .WithSummary("Soft-delete an unassigned forum board category.")
                .Produces(204).Produces(404).Produces(409).RequireAuthorization(ForumNames.Manage);
            forum.MapGet("/manage/users", ForumEndpoints.Rules)
                .WithSummary("List user moderation overrides.").RequireAuthorization(ForumNames.Manage);
            forum.MapPost("/manage/users", ForumEndpoints.SaveRule)
                .WithSummary("Set a user moderation override when enabled.").RequireAuthorization(ForumNames.Manage);
            MapLifecycle<ForumBoardCategory>(forum, "/categories", ForumNames.Manage, false);
            MapLifecycle<ForumBoard>(forum, "/boards", ForumNames.Manage);
            MapLifecycle<ForumPost>(forum, "/posts", ForumNames.Moderate, false);
            MapLifecycle<ForumUserModeration>(forum, "/manage/users", ForumNames.Manage);
        }
    }

    private static void MapLifecycle<T>(RouteGroupBuilder forum, string path, string grant, bool mapDelete = true)
    {
        if (mapDelete)
        {
            forum.MapPost(path + "/{id:guid}/delete", async (
                Guid id, IVersionedCommands<T> commands, CancellationToken cancellationToken) =>
                (await commands.Delete(id, cancellationToken)).ToNoContentResult())
                .WithSummary("Soft-delete a forum record.").Produces(204).Produces(404).RequireAuthorization(grant);
        }

        forum.MapPost(path + "/{id:guid}/restore", async (
            Guid id, IVersionedCommands<T> commands, CancellationToken cancellationToken) =>
            (await commands.Restore(id, cancellationToken)).ToNoContentResult())
            .WithSummary("Restore a deleted forum record (developers).").Produces(204).Produces(404)
            .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant);
        forum.MapPost(path + "/{id:guid}/hard/delete", async (
            Guid id, IVersionedCommands<T> commands, CancellationToken cancellationToken) =>
            (await commands.HardDelete(id, cancellationToken)).ToResult())
            .WithSummary("Permanently delete an unreferenced deleted forum record (developers).")
            .Produces(204).Produces(404).Produces(409)
            .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant);
    }
}
