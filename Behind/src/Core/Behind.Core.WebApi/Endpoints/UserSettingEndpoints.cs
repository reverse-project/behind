using System.ComponentModel.DataAnnotations;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public sealed class UserSettingEndpoints
{
    public sealed record UserSettingRequest([property: MaxLength(UserSettingKeys.MaxValueLength)] string? Value);

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Get(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        IUserSettingQueries userSettingQueries)
    {
        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session => Results.Json(await userSettingQueries.GetAll(session.UserId, httpContext.RequestAborted)));
    }

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Set(
        string key,
        UserSettingRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        IUserSettingCommands userSettingCommands,
        LocaleCatalog locales)
    {
        var value = request.Value?.Trim() ?? string.Empty;

        if (!UserSettingKeys.Known.Contains(key))
        {
            return Results.NotFound();
        }

        if (!await key.AcceptsAsync(value, locales, httpContext.RequestAborted))
        {
            return Results.BadRequest();
        }

        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                await userSettingCommands.Set(session.UserId, key, value, httpContext.RequestAborted);

                return Results.NoContent();
            });
    }
}
