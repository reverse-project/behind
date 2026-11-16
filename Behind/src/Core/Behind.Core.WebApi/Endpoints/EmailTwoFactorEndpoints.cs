using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class EmailTwoFactorEndpoints
{
    public sealed record AddressRequest([property: Required] string Ticket);
    public sealed record SendRequest([property: Required] string Ticket, [property: Required] string AddressId);
    public sealed record VerifyRequest([property: Required] string Ticket, [property: Required] string Code);
    public sealed record AddressResponse(string Id, string Value);
    public sealed record AddressesResponse(IReadOnlyList<AddressResponse> Addresses);
    public sealed record DeliveryResponse(string Ticket);

    [IgnoreETag]
    public static Task<IResult> Addresses(
        AddressRequest request, HttpContext context, ITwoFactorTickets tickets,
        UserManager<ApplicationUser> users, IEmailTwoFactor email) =>
        WithTicket(request.Ticket, context, tickets, users, async (user, ticket) =>
        {
            var result = await email.GetAddresses(user, ticket, context.RequestAborted);
            return result.Error is null
                ? Results.Json(new AddressesResponse(result.Addresses.Select(address =>
                    new AddressResponse(address.Id, address.Display)).ToList()))
                : Failure(result.Error);
        });

    [IgnoreETag]
    public static Task<IResult> Send(
        SendRequest request, HttpContext context, ITwoFactorTickets tickets,
        UserManager<ApplicationUser> users, IEmailTwoFactor email) =>
        WithTicket(request.Ticket, context, tickets, users, async (user, ticket) =>
        {
            var result = await email.Begin(user, ticket, request.AddressId, context.RequestAborted);
            return result.Error is null && result.Ticket is not null
                ? Results.Json(new DeliveryResponse(result.Ticket))
                : Failure(result.Error ?? EmailTwoFactor.DeliveryFailed);
        });

    [IgnoreETag]
    public static Task<IResult> Verify(
        VerifyRequest request, HttpContext context, ITwoFactorTickets tickets,
        UserManager<ApplicationUser> users, IEmailTwoFactor email, ITwoFactorSignIn signIn) =>
        WithTicket(request.Ticket, context, tickets, users, async (user, ticket) =>
        {
            var result = await email.Verify(user, ticket, request.Code, context.RequestAborted);
            return result.Error is null && result.Ticket is not null
                ? await signIn.Complete(user.Id, result.Ticket.Function, null, context, result.Ticket)
                : Failure(result.Error ?? TwoFactorFailure.InvalidCode);
        });

    private static async Task<IResult> WithTicket(
        string token, HttpContext context, ITwoFactorTickets tickets, UserManager<ApplicationUser> users,
        Func<ApplicationUser, TwoFactorTicket, Task<IResult>> action)
    {
        context.Response.Headers.CacheControl = "no-store";
        var ticket = tickets.Open(token).Where(found => found.Stage is TwoFactorStage.Verify or TwoFactorStage.Setup);
        return await ticket.Match(
            () => Task.FromResult(TwoFactorSignInEndpoints.TicketRejected()),
            async found =>
            {
                var user = await users.FindByIdAsync(found.UserId.ToString());
                return user is null ? Results.Unauthorized() : await action(user, found);
            });
    }

    private static IResult Failure(string code) =>
        Results.Json(new TwoFactorFailure(code), statusCode: code switch
        {
            TwoFactorFailure.TicketInvalid => StatusCodes.Status401Unauthorized,
            EmailTwoFactor.Unavailable => StatusCodes.Status404NotFound,
            EmailTwoFactor.RateLimited => StatusCodes.Status429TooManyRequests,
            EmailTwoFactor.DeliveryFailed => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status403Forbidden
        });
}
