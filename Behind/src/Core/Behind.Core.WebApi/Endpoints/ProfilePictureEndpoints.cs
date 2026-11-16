using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public static class ProfilePictureEndpoints
{
    public const long MaxBytes = 2 * 1024 * 1024;

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Get(HttpContext http, IModuleIdentity identity, IModuleUsers users)
    {
        http.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        if (await identity.UserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var profile = (await users.Find([userId], http.RequestAborted)).SingleOrDefault();

        return profile is null ? Results.NotFound() : Results.Ok(new
        {
            profile.PictureId,
            Url = profile.PictureId is { } id ? $"/profile/images/{id}" : null
        });
    }

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Upload(
        [FromForm] IFormFile? file, HttpContext http, IModuleIdentity identity,
        IModuleFiles files, UserProfiles profiles)
    {
        if (await identity.UserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        return await http.StoreImage(
            file, MaxBytes,
            async upload =>
            {
                var id = await files.Add(upload, http.RequestAborted);

                if (!await profiles.SetPicture(userId, id, http.RequestAborted))
                {
                    throw new InvalidOperationException("The profile picture owner no longer exists.");
                }

                return id;
            },
            id => new ImageUploadResponse(id, $"/profile/images/{id}"),
            limit => new ImageLimitResponse(limit),
            UserProfiles.FileType);
    }

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Remove(HttpContext http, IModuleIdentity identity, UserProfiles profiles)
    {
        if (await identity.UserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await profiles.SetPicture(userId, null, http.RequestAborted)).ToNoContentResult();
    }

    [IgnoreETag]
    public static async Task<IResult> Image(Guid id, HttpContext http, IModuleFiles files, UserProfiles profiles)
    {
        if (!await profiles.IsCurrentPicture(id, http.RequestAborted))
        {
            return Results.NotFound();
        }

        return http.ServeImage(await files.Open(UserProfiles.FileType, id, http.RequestAborted));
    }
}
