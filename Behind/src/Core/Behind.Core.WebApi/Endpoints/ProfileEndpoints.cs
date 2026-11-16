using System.ComponentModel.DataAnnotations;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Endpoints;

public sealed class ProfileEndpoints
{
    public sealed record DisplayNameRequest([property: MaxLength(DisplayNameExtensions.MaxLength)] string? DisplayName);

    public sealed record NewPasswordRequest([property: Required] string Password);

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> ChangePassword(
        NewPasswordRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        UserManager<ApplicationUser> userManager,
        ILogger<ProfileEndpoints> logger)
    {
        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                if (session.IsImpersonated)
                {
                    logger.LogWarning(
                        "Password change rejected: session {SessionId} is an impersonation session",
                        () => [session.SessionId]);

                    return Results.Forbid();
                }

                var user = await userManager.FindByIdAsync(session.UserId.ToString());

                if (user is null)
                {
                    return Results.Unauthorized();
                }

                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var result = await userManager.ResetPasswordAsync(user, token, request.Password);

                if (result.Succeeded)
                {
                    logger.LogInformation("User {UserId} changed the password", () => [user.Id]);

                    return Results.NoContent();
                }

                logger.LogWarning(
                    "Password change of user {UserId} failed with {ErrorCodes}",
                    () => [user.Id, result.Errors.Select(e => e.Code).ToArray()]);

                return Results.BadRequest(result.Errors.Select(e => e.Description));
            });
    }

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
