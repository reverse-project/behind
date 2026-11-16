using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Sport.Common;
using Behind.Sport.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Sport.WebApi.Endpoints;

public static class SportImageEndpoints
{
    [IgnoreETag]
    public static Task<IResult> UploadTeamLogo(IFormFile? file, HttpContext httpContext, IModuleFiles files) =>
        Upload(SportFileTypes.Teams, SportFileTypes.TeamImage, file, httpContext, files);

    [IgnoreETag]
    public static Task<IResult> UploadPlayerPicture(IFormFile? file, HttpContext httpContext, IModuleFiles files) =>
        Upload(SportFileTypes.Players, SportFileTypes.PlayerImage, file, httpContext, files);

    [IgnoreETag]
    public static Task<IResult> GetTeamLogo(Guid id, HttpContext httpContext, IModuleFiles files) =>
        Serve(SportFileTypes.Teams, id, httpContext, files);

    [IgnoreETag]
    public static Task<IResult> GetPlayerPicture(Guid id, HttpContext httpContext, IModuleFiles files) =>
        Serve(SportFileTypes.Players, id, httpContext, files);

    private static Task<IResult> Upload(
        string type,
        Func<Guid, string> urlOf,
        IFormFile? file,
        HttpContext httpContext,
        IModuleFiles files) =>
        httpContext.StoreImage(
            file,
            SportModuleNames.ImageLimit,
            upload => files.Add(upload, httpContext.RequestAborted),
            id => new ImageUploadResponse(id, urlOf(id)),
            maxBytes => new ImageLimitResponse(maxBytes),
            type);

    private static async Task<IResult> Serve(string type, Guid id, HttpContext httpContext, IModuleFiles files) =>
        httpContext.ServeImage(await files.Open(type, id, httpContext.RequestAborted));
}
