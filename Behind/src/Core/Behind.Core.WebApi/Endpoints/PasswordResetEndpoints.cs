using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class PasswordResetEndpoints
{
    public const string Route = "/sign/in/password/reset";

    private const string NoStore = "no-store";

    public sealed record PasswordResetRequest(
        [property: Required] string Token,
        [property: Required] string Password);

    public sealed record PasswordResetResponse(
        string ResetToken,
        DateTimeOffset Expires,
        MagicLinkFunction Function = MagicLinkFunction.LostPassword,
        IReadOnlyList<string>? RecoveryCodes = null);

    [IgnoreETag]
    public static async Task<IResult> Reset(UserManager<ApplicationUser> userManager,
        IPasswordResetGrants passwordResetGrants, IMagicLinkQueries magicLinkQueries,
        IMagicLinkCommands magicLinkCommands, ISessionCommands sessionCommands, TimeProvider timeProvider,
        HttpContext httpContext, ILogger<PasswordResetEndpoints> logger, PasswordResetRequest request)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var now = timeProvider.GetUtcNow();
        var cancellationToken = httpContext.RequestAborted;
        var nonce = passwordResetGrants.Verify(request.Token);

        var userId = await nonce.Match(
            none: () => Task.FromResult<Option<Guid>>(None),
            some: n => magicLinkQueries.FindPasswordResetUser(n, now, cancellationToken));

        var user = await userId.Match(
            none: () => Task.FromResult<ApplicationUser?>(null),
            some: id => userManager.FindByIdAsync(id.ToString()));

        if (user is null)
        {
            logger.LogWarning("Password reset rejected: the grant is malformed, unknown, expired or already used");

            return Results.Unauthorized();
        }

        var errors = new List<IdentityError>();
        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(userManager, user, request.Password);
            errors.AddRange(validation.Errors);
        }

        if (errors.Count > 0)
        {
            logger.LogWarning(
                "Password reset of user {UserId} rejected with {ErrorCodes}, the grant stays usable",
                () => [user.Id, errors.Select(e => e.Code).ToArray()]);

            return Results.BadRequest(errors.Select(e => e.Description));
        }

        var consumed = await nonce.Match(
            none: () => Task.FromResult<Option<Guid>>(None),
            some: n => magicLinkCommands.ConsumePasswordReset(n, now, cancellationToken));

        if (!consumed.Match(() => false, id => id == user.Id))
        {
            logger.LogWarning("Password reset of user {UserId} rejected: the grant was used concurrently", () => [user.Id]);

            return Results.Unauthorized();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.Password);

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Password reset of user {UserId} failed with {ErrorCodes}",
                () => [user.Id, result.Errors.Select(e => e.Code).ToArray()]);

            return Results.BadRequest(result.Errors.Select(e => e.Description));
        }

        var revoked = await magicLinkCommands.RevokeForUser(user.Id, now, cancellationToken);
        await sessionCommands.InvalidateAllSessions(user.Id, user.Id, now, cancellationToken);

        logger.LogInformation(
            "User {UserId} reset the password, {Revoked} outstanding links revoked and all sessions signed out",
            () => [user.Id, revoked]);

        return Results.NoContent();
    }
}
