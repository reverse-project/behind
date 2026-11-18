using Behind.Core.Database;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public sealed class EmailTwoFactorAttemptFilter(
    ITwoFactorTickets tickets,
    IMagicLinkCommands commands,
    TimeProvider timeProvider) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<EmailTwoFactorEndpoints.VerifyRequest>().Single();
        var ticket = tickets.Open(request.Ticket).Match<TwoFactorTicket?>(() => null, found => found);
        if (ticket is null || ticket.Stage is not (TwoFactorStage.Verify or TwoFactorStage.Setup))
        {
            return TwoFactorSignInEndpoints.TicketRejected();
        }

        if (!await EmailTwoFactor.ReserveVerificationAttempt(
                commands, ticket.UserId, timeProvider.GetUtcNow(), context.HttpContext.RequestAborted))
        {
            return Results.Json(new TwoFactorFailure(EmailTwoFactor.RateLimited),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        context.HttpContext.Features.Set(new EmailTwoFactorVerificationAttempt(ticket.UserId));
        return await next(context);
    }
}
