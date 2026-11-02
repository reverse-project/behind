using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class ArticleRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapArticleEndpoints()
        {
            var read = GrantName.Of(GrantArea.Article, GrantAction.Read);
            var write = GrantName.Of(GrantArea.Article, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Article, GrantAction.Delete);

            application.MapGet("/articles", ArticleEndpoints.List)
                .Produces<PublicArticleListResponse>()
                .Produces(400)
                .Produces(500);

            application.MapGet("/articles/categories", ArticleCategoryEndpoints.ListActive)
                .Produces<IReadOnlyList<NewsCategoryItem>>()
                .Produces(500);

            application.MapGet("/articles/manage", ArticleEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<ArticleManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/articles/categories/manage", ArticleCategoryEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<ArticleCategoryManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/articles/{id:guid}/versions", ArticleEndpoints.GetHistory)
                .RequireAuthorization(read)
                .Produces<ArticleHistoryResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapGet("/articles/{id:guid}/versions/{version:long}", ArticleEndpoints.GetVersion)
                .RequireAuthorization(read)
                .Produces<ArticleDetailResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/articles/images", SiteImageEndpoints.Upload)
                .RequireAuthorization(write)
                .DisableAntiforgery()
                .Produces<SiteImageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<SiteImageLimitResponse>(413)
                .Produces(415)
                .Produces(500);

            application.MapGet("/articles/{id:guid}", ArticleEndpoints.Get)
                .Produces<ArticleDetailResponse>()
                .Produces(404)
                .Produces(500);

            application.MapPost("/articles", ArticleEndpoints.Create)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<ArticleEndpoints.SaveArticleRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/articles/{id:guid}/versions", ArticleEndpoints.SaveVersion)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<ArticleEndpoints.SaveArticleRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/articles/{id:guid}/versions/{version:long}/activate", ArticleEndpoints.Activate)
                .RequireAuthorization(write)
                .Produces<NewsSavedResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/articles/{id:guid}/delete", ArticleEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/articles/{id:guid}/restore", ArticleEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/articles/{id:guid}/hard/delete", ArticleEndpoints.HardDelete)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapPost("/articles/categories", ArticleCategoryEndpoints.Create)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<ArticleCategoryEndpoints.SaveArticleCategoryRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/articles/categories/{id:guid}", ArticleCategoryEndpoints.Update)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<ArticleCategoryEndpoints.SaveArticleCategoryRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/articles/categories/{id:guid}/delete", ArticleCategoryEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/articles/categories/{id:guid}/restore", ArticleCategoryEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/articles/categories/{id:guid}/hard/delete", ArticleCategoryEndpoints.HardDelete)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);
        }
    }
}
