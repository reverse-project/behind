using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Oidc;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class OidcSignInEndpoints
{
    public sealed record ProviderResponse(string Id, string DisplayName);
    public sealed record RedeemRequest([property: Required] string Ticket);

    [IgnoreETag]
    public static IResult Providers(OidcProviderCatalog catalog, IOptions<AccountsConfiguration> accounts,
        IOptions<SecurityConfiguration> security, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Json(catalog.IsEnabled && accounts.Value.SignIn && security.Value.Jwt.Match(() => false, _ => true)
            ? catalog.Providers.Select(provider => new ProviderResponse(provider.Id, provider.DisplayName)).ToArray()
            : []);
    }

    [IgnoreETag]
    public static IResult Challenge(string id, OidcProviderCatalog catalog,
        IOptions<AccountsConfiguration> accounts, IOptions<SecurityConfiguration> security,
        HttpContext context, ILogger<OidcSignInEndpoints> logger)
    {
        if (catalog.Find(id) is null || !accounts.Value.SignIn || !security.Value.Jwt.Match(() => false, _ => true))
        {
            logger.LogWarning("OIDC challenge rejected: provider or sign-in is unavailable");
            return Results.NotFound();
        }

        if (!context.Request.IsHttps)
        {
            logger.LogWarning("OIDC challenge rejected: HTTPS is required");
            return Results.BadRequest();
        }

        var flow = RandomNumberGenerator.GetHexString(16);
        var proof = RandomNumberGenerator.GetHexString(32);
        context.Response.Cookies.Append(OidcExtensions.CookieName(flow), proof,
            OidcExtensions.BrowserCookie(catalog.Configuration.AuthenticationTimeout + catalog.Configuration.TicketLifetime));
        context.Response.Headers.CacheControl = "no-store";
        var properties = new AuthenticationProperties();
        properties.Items[OidcExtensions.FlowId] = flow;
        properties.Items[OidcExtensions.Proof] = proof;
        return Results.Challenge(properties, [OidcProviderCatalog.Scheme(id)]);
    }

    [IgnoreETag]
    public static async Task<IResult> Redeem(RedeemRequest request, OidcTicketStore tickets,
        IOidcSignInService signIn, IOptions<AccountsConfiguration> accounts,
        IOptions<SecurityConfiguration> security, ISessionCommands sessions, IWallOfShameQueries bans,
        ITwoFactorGate twoFactor, TimeProvider timeProvider, HttpContext context,
        ILogger<OidcSignInEndpoints> logger)
    {
        context.Response.Headers.CacheControl = "no-store";
        var jwt = security.Value.Jwt.Match<JwtSettings?>(() => null, value => value);
        if (!tickets.IsEnabled || !accounts.Value.SignIn || jwt is null)
        {
            logger.LogWarning("OIDC ticket redemption rejected: sign-in is unavailable");
            return Results.NotFound();
        }

        var ticket = tickets.Redeem(request.Ticket, flow => context.Request.Cookies[OidcExtensions.CookieName(flow)]);
        if (ticket is null)
        {
            logger.LogWarning("OIDC ticket redemption rejected: invalid, expired or unbound ticket");
            return Results.Unauthorized();
        }

        context.Response.Cookies.Delete(OidcExtensions.CookieName(ticket.FlowId), OidcExtensions.BrowserCookie(TimeSpan.Zero));
        var user = await signIn.Resolve(ticket.Identity, context.RequestAborted);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return await SignInEndpoints.StartSession(user, jwt, security.Value.EmailConfirmation,
            sessions, bans, timeProvider.GetUtcNow(), context, logger, Some(twoFactor),
            primaryOidcProviderId: ticket.Identity.ProviderId);
    }
}
