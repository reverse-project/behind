using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public enum AccountMode
{
    Password,
    MagicLink,
    TwoFactor,
    Passkey,
    PrimaryPasskey,
    EmailTwoFactor
}

public static class AccountModeExtensions
{
    extension(RouteHandlerBuilder builder)
    {
        public RouteHandlerBuilder RequireAccountMode(AccountMode mode) =>
            builder.AddEndpointFilter(new AccountModeFilter(mode));
    }
}

public sealed class AccountModeFilter(AccountMode mode) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var settings = services.GetRequiredService<IOptionsSnapshot<AccountsConfiguration>>().Value;
        var enabled = mode switch
        {
            AccountMode.Password => settings.Password,
            AccountMode.MagicLink => settings.MagicLink
                || context.Arguments.OfType<SignInEndpoints.MagicLinkRequest>()
                    .Any(request => request.Function == MagicLinkFunction.LostPassword),
            AccountMode.TwoFactor => settings.TwoFactor,
            AccountMode.Passkey => settings.TwoFactor && settings.Passkey,
            AccountMode.PrimaryPasskey => settings.Passkey,
            AccountMode.EmailTwoFactor => settings.TwoFactor && settings.EmailTwoFactor,
            _ => false
        };

        if (enabled && mode is (AccountMode.TwoFactor or AccountMode.Passkey or AccountMode.EmailTwoFactor)
            && (!settings.MagicLink || !settings.Password || !settings.Passkey))
        {
            var token = context.Arguments.Select(argument => argument switch
            {
                TwoFactorSignInEndpoints.TicketRequest request => request.Ticket,
                TwoFactorSignInEndpoints.CodeRequest request => request.Ticket,
                TwoFactorSignInEndpoints.PasskeyRequest request => request.Ticket,
                TwoFactorSignInEndpoints.PasskeyRegistrationRequest request => request.Ticket,
                EmailTwoFactorEndpoints.AddressRequest request => request.Ticket,
                EmailTwoFactorEndpoints.SendRequest request => request.Ticket,
                EmailTwoFactorEndpoints.VerifyRequest request => request.Ticket,
                _ => null
            }).FirstOrDefault(value => value is not null);
            if (token is not null)
            {
                enabled = services.GetRequiredService<ITwoFactorTickets>().Open(token)
                    .Match(() => true, ticket => ticket.Function switch
                    {
                        null => ticket.PrimaryCredentialId is null ? settings.Password : settings.Passkey,
                        MagicLinkFunction.SignIn => settings.MagicLink,
                        _ => true
                    });
            }
        }

        if (enabled)
        {
            return await next(context);
        }

        services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AccountModeExtensions))
            .LogWarning("Authentication method {Mode} rejected because it is disabled", mode);
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return Results.NotFound();
    }
}
