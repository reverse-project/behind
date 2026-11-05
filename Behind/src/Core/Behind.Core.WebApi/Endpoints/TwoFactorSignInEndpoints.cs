using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class TwoFactorSignInEndpoints
{
    public sealed record TicketRequest([property: Required] string Ticket);

    public sealed record CodeRequest([property: Required] string Ticket, [property: Required] string Code);

    public sealed record PasskeyRequest(
        [property: Required] string Ticket,
        [property: Required] AuthenticatorAssertionRawResponse Credential);

    public sealed record PasskeyRegistrationRequest(
        [property: Required] string Ticket,
        [property: Required] AuthenticatorAttestationRawResponse Credential,
        [property: MaxLength(UserPasskey.MaxNameLength)] string? Name = null);

    [IgnoreETag]
    public static Task<IResult> VerifyTotp(
        CodeRequest request,
        HttpContext httpContext,
        ITwoFactorTickets tickets,
        ITwoFactorVerification verification,
        ITwoFactorSignIn signIn,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithTicket(request.Ticket, TwoFactorStage.Verify, tickets, logger, async ticket =>
            await verification.VerifyTotp(ticket.UserId, request.Code, httpContext.RequestAborted)
                ? await signIn.Complete(ticket.UserId, ticket.Function, null, httpContext)
                : Rejected(logger, ticket, "authenticator code"));

    [IgnoreETag]
    public static Task<IResult> VerifyRecoveryCode(
        CodeRequest request,
        HttpContext httpContext,
        ITwoFactorTickets tickets,
        ITwoFactorVerification verification,
        ITwoFactorSignIn signIn,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithTicket(request.Ticket, TwoFactorStage.Verify, tickets, logger, async ticket =>
        {
            var outcome = await verification.VerifyRecoveryCode(ticket.UserId, request.Code, httpContext.RequestAborted);

            return await outcome.Match(
                () => Task.FromResult(Rejected(logger, ticket, "recovery code")),
                used =>
                {
                    logger.LogInformation(
                        "Recovery code used by user {UserId}, new codes issued: {Issued}",
                        () => [ticket.UserId, used.NewCodes.Count > 0]);

                    return signIn.Complete(ticket.UserId, ticket.Function, used.NewCodes, httpContext);
                });
        });

    [IgnoreETag]
    public static Task<IResult> PasskeyOptions(
        TicketRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        ITwoFactorTickets tickets,
        ITwoFactorVerification verification,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithTicket(request.Ticket, TwoFactorStage.Verify, tickets, logger, async ticket =>
        {
            var options = await verification.BeginPasskey(
                httpContext.PasskeyOrigin(configuration),
                ticket.UserId,
                httpContext.RequestAborted);

            return options.Match<IResult>(
                () => Results.NotFound(),
                found => Results.Json(new PasskeyAssertionChallenge(
                    tickets.Seal(tickets.Create(ticket.UserId, ticket.Stage, ticket.Function, found.ToJson())),
                    found)));
        });

    [IgnoreETag]
    public static Task<IResult> VerifyPasskey(
        PasskeyRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        ITwoFactorTickets tickets,
        ITwoFactorVerification verification,
        ITwoFactorSignIn signIn,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithTicket(request.Ticket, TwoFactorStage.Verify, tickets, logger, async ticket =>
        {
            var options = Try(() => AssertionOptions.FromJson(ticket.Ceremony))
                .Run()
                .Match<Option<AssertionOptions>>(_ => None, parsed => parsed);

            return await options.Match(
                () => Task.FromResult(Rejected(logger, ticket, "passkey")),
                async parsed => await verification.VerifyPasskey(
                    httpContext.PasskeyOrigin(configuration),
                    ticket.UserId,
                    parsed,
                    request.Credential,
                    httpContext.RequestAborted)
                    ? await signIn.Complete(ticket.UserId, ticket.Function, null, httpContext)
                    : Rejected(logger, ticket, "passkey"));
        });

    [IgnoreETag]
    public static Task<IResult> SetupTotp(
        TicketRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ITwoFactorTickets tickets,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithSetupUser(request.Ticket, tickets, userManager, logger, async (user, _) =>
            (await enrollment.BeginTotp(user, httpContext.RequestAborted)).Match<IResult>(
                () => Results.Conflict(new TwoFactorFailure(TwoFactorFailure.AlreadyEnabled)),
                setup => Results.Json(setup)));

    [IgnoreETag]
    public static Task<IResult> ConfirmTotp(
        CodeRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ITwoFactorTickets tickets,
        ITwoFactorEnrollment enrollment,
        ITwoFactorSignIn signIn,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithSetupUser(request.Ticket, tickets, userManager, logger, async (user, ticket) =>
        {
            var enrolled = await enrollment.ConfirmTotp(user.Id, request.Code, httpContext.RequestAborted);

            return await enrolled.Match(
                () => Task.FromResult(Rejected(logger, ticket, "authenticator code")),
                done => signIn.Complete(user.Id, ticket.Function, done.RecoveryCodes, httpContext));
        });

    [IgnoreETag]
    public static Task<IResult> SetupPasskeyOptions(
        TicketRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ITwoFactorTickets tickets,
        ITwoFactorEnrollment enrollment,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithSetupUser(request.Ticket, tickets, userManager, logger, async (user, ticket) =>
            Results.Json(await enrollment.BeginPasskey(
                httpContext.PasskeyOrigin(configuration),
                user,
                TwoFactorStage.Setup,
                ticket.Function,
                httpContext.RequestAborted)));

    [IgnoreETag]
    public static Task<IResult> SetupPasskey(
        PasskeyRegistrationRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        ITwoFactorTickets tickets,
        ITwoFactorEnrollment enrollment,
        ITwoFactorSignIn signIn,
        ILogger<TwoFactorSignInEndpoints> logger) =>
        WithTicket(request.Ticket, TwoFactorStage.Setup, tickets, logger, async ticket =>
        {
            var enrolled = await enrollment.CompletePasskey(
                httpContext.PasskeyOrigin(configuration),
                ticket,
                request.Name,
                request.Credential,
                httpContext.RequestAborted);

            return await enrolled.Match(
                () => Task.FromResult(Rejected(logger, ticket, "passkey")),
                done => signIn.Complete(ticket.UserId, ticket.Function, done.RecoveryCodes, httpContext));
        });

    private static async Task<IResult> WithTicket(
        string token,
        TwoFactorStage stage,
        ITwoFactorTickets tickets,
        ILogger logger,
        Func<TwoFactorTicket, Task<IResult>> action)
    {
        var ticket = tickets.Open(token).Where(opened => opened.Stage == stage);

        return await ticket.Match(
            () =>
            {
                logger.LogWarning("Two-factor step rejected: the ticket is invalid, expired or of another stage");

                return Task.FromResult(TicketRejected());
            },
            action);
    }

    private static Task<IResult> WithSetupUser(
        string token,
        ITwoFactorTickets tickets,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        Func<ApplicationUser, TwoFactorTicket, Task<IResult>> action) =>
        WithTicket(token, TwoFactorStage.Setup, tickets, logger, async ticket =>
        {
            var user = await userManager.FindByIdAsync(ticket.UserId.ToString());

            return user is null ? Results.Unauthorized() : await action(user, ticket);
        });

    internal static IResult TicketRejected() =>
        Results.Json(
            new TwoFactorFailure(TwoFactorFailure.TicketInvalid),
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Rejected(ILogger logger, TwoFactorTicket ticket, string method)
    {
        logger.LogWarning(
            "Second factor ({Method}) rejected for user {UserId}",
            () => [method, ticket.UserId]);

        return Results.Json(new TwoFactorFailure(TwoFactorFailure.InvalidCode), statusCode: StatusCodes.Status403Forbidden);
    }
}
