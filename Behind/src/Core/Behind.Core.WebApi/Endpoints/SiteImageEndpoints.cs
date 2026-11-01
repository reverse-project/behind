using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SiteImageEndpoints
{
    public const string ImagePath = "/sites/images";
    public const string ImmutableCache = "public, max-age=31536000, immutable";
    public const string NoSniff = "nosniff";

    [IgnoreETag]
    public static async Task<IResult> Upload(
        IFormFile? file,
        HttpContext httpContext,
        ISiteImageCommands commands,
        TimeProvider timeProvider,
        IOptionsSnapshot<SitesConfiguration> configuration)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest();
        }

        var limit = configuration.Value.ImageLimit;

        if (file.Length > limit)
        {
            return Results.Json(new SiteImageLimitResponse(limit), statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var content = await file.ReadAllBytesAsync(httpContext.RequestAborted);

        return await content.DetectImageContentType().Match(
            () => Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status415UnsupportedMediaType)),
            async contentType =>
            {
                var image = new SiteImage(
                    file.FileName.ToSiteImageFileName("image"),
                    contentType,
                    content,
                    timeProvider.GetUtcNow());

                var id = await commands.Add(image, httpContext.RequestAborted);

                return Results.Json(new SiteImageResponse(id, $"{ImagePath}/{id}"));
            });
    }

    [IgnoreETag]
    public static async Task<IResult> Get(Guid id, HttpContext httpContext, ISiteImageQueries queries)
    {
        var image = await queries.Find(id, httpContext.RequestAborted);

        return image.Match(
            () => Results.NotFound(),
            found =>
            {
                httpContext.Response.Headers.CacheControl = ImmutableCache;
                httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = NoSniff;

                return Results.File(found.Content, found.ContentType);
            });
    }
}
