using System.ComponentModel.DataAnnotations;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public sealed class ProfileEndpoints
{
    public sealed record DisplayNameRequest([MaxLength(DisplayNameExtensions.MaxLength)] string? DisplayName);

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> ChangeDisplayName(
        DisplayNameRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        IUserCommands userCommands)
    {
        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                var changed = await userCommands.ChangeDisplayName(
                    session.UserId,
                    request.DisplayName.ToDisplayName(),
                    httpContext.RequestAborted);

                return changed ? Results.NoContent() : Results.Unauthorized();
            });
    }
}
