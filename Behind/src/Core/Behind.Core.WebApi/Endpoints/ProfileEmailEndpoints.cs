using System.ComponentModel.DataAnnotations;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class ProfileEmailEndpoints
{
    public sealed record AddressRequest([property: Required, EmailAddress, MaxLength(255)] string Email);
    public sealed record AddressesResponse(IReadOnlyList<IEmailQueries.Address> Items, bool ConfirmationRequired);
    public sealed record Failure(string Code);
    public sealed record SavedAddress(IEmailQueries.Address Address, bool ConfirmationRequired, bool EmailSent);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Get(
        HttpContext httpContext, ISessionQueries sessions, IEmailQueries emails,
        UserManager<ApplicationUser> manager, IOptions<SecurityConfiguration> security,
        ILogger<ProfileEmailEndpoints> logger) =>
        ForSession(httpContext, sessions, manager, logger, async user =>
            Results.Json(new AddressesResponse(await emails.Addresses(user.Id, httpContext.RequestAborted),
                security.Value.EmailConfirmation)));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Save(
        Guid? id, AddressRequest request, HttpContext httpContext, ISessionQueries sessions,
        IEmailCommands commands, UserManager<ApplicationUser> manager,
        IOptions<SecurityConfiguration> security, IEmailSender<ApplicationUser> sender,
        IConfiguration configuration, ILogger<ProfileEmailEndpoints> logger) =>
        ForSession(httpContext, sessions, manager, logger, async user =>
        {
            var required = security.Value.EmailConfirmation;
            var result = await commands.SaveAddress(user.Id, id, request.Email, required, httpContext.RequestAborted);

            if (result.Address is not { } address)
            {
                logger.LogWarning("Email change of user {UserId} rejected: {Status}", () => [user.Id, result.Status]);
                return result.Status == IEmailCommands.AddressStatus.NotFound
                    ? Results.NotFound()
                    : Results.Conflict(new Failure("emailInUse"));
            }

            var needsConfirmation = required && address.ConfirmedAt is null;
            var sent = needsConfirmation
                && await manager.SendAddressConfirmation(user, address, sender, configuration, httpContext, logger);
            logger.LogInformation("User {UserId} saved email {EmailId}", () => [user.Id, address.Id]);
            var response = new SavedAddress(address, needsConfirmation, sent);

            return id is null
                ? Results.Created("/profile/emails", response)
                : Results.Ok(response);
        });

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Remove(
        Guid id, HttpContext httpContext, ISessionQueries sessions, IEmailCommands commands,
        UserManager<ApplicationUser> manager, ILogger<ProfileEmailEndpoints> logger) =>
        ForSession(httpContext, sessions, manager, logger, async user =>
        {
            var status = await commands.RemoveAddress(user.Id, id, httpContext.RequestAborted);
            logger.Log(status == IEmailCommands.AddressStatus.Saved ? LogLevel.Information : LogLevel.Warning,
                "Email removal by user {UserId}: {Status}", () => [user.Id, status]);
            return status switch
            {
                IEmailCommands.AddressStatus.Saved => Results.NoContent(),
                IEmailCommands.AddressStatus.NotFound => Results.NotFound(),
                _ => Results.Conflict(new Failure("lastEmail"))
            };
        });

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Resend(
        Guid id, HttpContext httpContext, ISessionQueries sessions, IEmailQueries emails,
        UserManager<ApplicationUser> manager, IOptions<SecurityConfiguration> security,
        IEmailSender<ApplicationUser> sender, IConfiguration configuration, ILogger<ProfileEmailEndpoints> logger) =>
        ForSession(httpContext, sessions, manager, logger, async user =>
        {
            var address = (await emails.Addresses(user.Id, httpContext.RequestAborted)).SingleOrDefault(item => item.Id == id);

            if (!security.Value.EmailConfirmation || address is null)
            {
                logger.LogWarning("Email confirmation resend by user {UserId} rejected", () => [user.Id]);
                return Results.NotFound();
            }

            if (address.ConfirmedAt is not null)
            {
                return Results.NoContent();
            }

            return await manager.SendAddressConfirmation(user, address, sender, configuration, httpContext, logger)
                ? Results.NoContent()
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });

    private static async Task<IResult> ForSession(
        HttpContext httpContext, ISessionQueries sessions, UserManager<ApplicationUser> manager,
        ILogger logger, Func<ApplicationUser, Task<IResult>> action)
    {
        var current = await httpContext.FindCurrentSession(sessions);
        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                if (session.IsImpersonated)
                {
                    logger.LogWarning("Email management rejected: session {SessionId} is impersonated", () => [session.SessionId]);
                    return Results.Forbid();
                }

                var user = await manager.FindByIdAsync(session.UserId.ToString());
                return user is null ? Results.Unauthorized() : await action(user);
            });
    }
}
