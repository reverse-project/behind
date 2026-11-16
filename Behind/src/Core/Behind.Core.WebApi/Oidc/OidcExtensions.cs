using Behind.Core.Common.Configuration;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Behind.Core.WebApi.Oidc;

public static class OidcExtensions
{
    internal const string FlowId = "Behind.Oidc.Flow";
    internal const string Proof = "Behind.Oidc.Proof";
    private const string CookieScheme = "Behind.Oidc.Unused";

    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddOidc()
        {
            var catalog = new OidcProviderCatalog(
                builder.Configuration.GetSection(OidcConfiguration.SectionName).Get<OidcConfiguration>() ?? new());
            builder.Services.AddSingleton(services => new OidcProviderCatalog(catalog.Configuration,
                services.GetRequiredService<IOptionsMonitor<AccountsConfiguration>>()));
            builder.Services.AddSingleton<OidcTicketStore>();
            builder.Services.AddScoped<IOidcSignInService, OidcSignInService>();
            builder.Services.AddScoped<Behind.Core.Database.IOidcAccountStore, Behind.Core.Database.OidcAccountStore>();
            if (catalog.Providers.Count == 0)
            {
                return builder;
            }

            var frontend = builder.Configuration.FrontendUri().Match<Uri?>(() => null, value => value)
                ?? throw new InvalidOperationException("OIDC requires a valid Frontend:Url.");
            if (frontend.Scheme != "https")
            {
                throw new InvalidOperationException("OIDC requires an HTTPS Frontend:Url for browser-bound cookies.");
            }

            var authentication = builder.Services.AddAuthentication().AddCookie(CookieScheme);
            foreach (var provider in catalog.Providers)
            {
                authentication.AddOpenIdConnect(OidcProviderCatalog.Scheme(provider.Id), options =>
                {
                    options.SignInScheme = CookieScheme;
                    options.Authority = provider.Authority;
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.CallbackPath = OidcProviderCatalog.CallbackPath(provider.Id);
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.MapInboundClaims = false;
                    options.ClaimActions.Remove("iss");
                    options.SaveTokens = false;
                    options.RemoteAuthenticationTimeout = catalog.Configuration.AuthenticationTimeout;
                    options.Scope.Clear();
                    foreach (var scope in provider.Scopes)
                    {
                        options.Scope.Add(scope);
                    }

                    options.Events = new OpenIdConnectEvents
                    {
                        OnTicketReceived = context =>
                        {
                            var accounts = context.HttpContext.RequestServices.GetRequiredService<IOptions<AccountsConfiguration>>();
                            var security = context.HttpContext.RequestServices.GetRequiredService<IOptions<SecurityConfiguration>>();
                            if (!context.HttpContext.RequestServices.GetRequiredService<OidcProviderCatalog>().IsEnabled
                                || !accounts.Value.SignIn || !security.Value.Jwt.Match(() => false, _ => true)
                                || context.Principal is null
                                || context.Properties is null
                                || !context.Properties.Items.TryGetValue(FlowId, out var flow) || flow is null
                                || !context.Properties.Items.TryGetValue(Proof, out var proof) || proof is null
                                || context.Request.Cookies[CookieName(flow)] != proof)
                            {
                                Reject(context.HttpContext, frontend, provider.Id, "unavailable sign-in or lost browser binding");
                                context.HandleResponse();
                                return Task.CompletedTask;
                            }

                            var store = context.HttpContext.RequestServices.GetRequiredService<OidcTicketStore>();
                            string ticket;
                            try
                            {
                                ticket = store.Issue(OidcIdentity.FromPrincipal(provider, context.Principal), flow, proof);
                            }
                            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
                            {
                                Reject(context.HttpContext, frontend, provider.Id, exception.GetType().Name);
                                context.HandleResponse();
                                return Task.CompletedTask;
                            }
                            context.Response.Headers.CacheControl = "no-store";
                            context.Response.Headers["Referrer-Policy"] = "no-referrer";
                            context.Response.Redirect(frontend.ToFrontendLink("/signin",
                                [new KeyValuePair<string, string?>("oidc", ticket)]));
                            context.HandleResponse();
                            return Task.CompletedTask;
                        },
                        OnRemoteFailure = context =>
                        {
                            Reject(context.HttpContext, frontend, provider.Id, context.Failure?.GetType().Name);
                            context.HandleResponse();
                            return Task.CompletedTask;
                        }
                    };
                });
            }

            return builder;
        }
    }

    private static void Reject(HttpContext context, Uri frontend, string providerId, string? reason)
    {
        context.RequestServices.GetRequiredService<ILogger<OidcTicketStore>>()
            .LogWarning("OIDC provider {ProviderId} sign-in failed ({Reason})", providerId, reason);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Redirect(frontend.ToFrontendLink("/signin",
            [new KeyValuePair<string, string?>("oidcError", "failed")]));
    }

    public static string CookieName(string flow) => $"Behind.Oidc.Flow.{flow}";

    public static CookieOptions BrowserCookie(TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Path = "/sign/in/oidc",
        MaxAge = lifetime,
        IsEssential = true
    };
}
