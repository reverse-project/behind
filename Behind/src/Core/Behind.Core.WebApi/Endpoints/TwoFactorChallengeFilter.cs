using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Endpoints;

public sealed class TwoFactorChallengeFilter(
    AbstractCoreContext database,
    IMagicLinkCommands commands,
    ITwoFactorTickets tickets,
    TimeProvider timeProvider,
    ILogger<TwoFactorChallengeFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var token = context.Arguments.Select(argument => argument switch
        {
            TwoFactorSignInEndpoints.TicketRequest request => request.Ticket,
            TwoFactorSignInEndpoints.CodeRequest request => request.Ticket,
            TwoFactorSignInEndpoints.PasskeyRequest request => request.Ticket,
            TwoFactorSignInEndpoints.PasskeyRegistrationRequest request => request.Ticket,
            EmailTwoFactorEndpoints.VerifyRequest request => request.Ticket,
            _ => null
        }).FirstOrDefault(value => value is not null);
        var ticket = tickets.Open(token)
            .Where(found => found.Stage is TwoFactorStage.Setup or TwoFactorStage.Verify && found.Nonce is not null);

        return await ticket.Match(
            () => Task.FromResult<object?>(TwoFactorSignInEndpoints.TicketRejected()),
            async found =>
            {
                await using var transaction = await database.Database.BeginTransactionAsync(context.HttpContext.RequestAborted);
                if (!await commands.LockForFunction(found.Nonce!.Value, found.UserId, MagicLinkFunction.TwoFactorChallenge,
                        timeProvider.GetUtcNow(), context.HttpContext.RequestAborted))
                {
                    logger.LogWarning("Second-factor step rejected for user {UserId}: challenge is no longer usable", found.UserId);
                    return TwoFactorSignInEndpoints.TicketRejected();
                }

                var result = await next(context);
                if (result is IStatusCodeHttpResult { StatusCode: StatusCodes.Status401Unauthorized })
                {
                    await transaction.RollbackAsync(context.HttpContext.RequestAborted);
                }
                else
                {
                    await transaction.CommitAsync(context.HttpContext.RequestAborted);
                }

                return result;
            });
    }
}
