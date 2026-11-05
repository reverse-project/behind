using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class TwoFactorRoutesExtensions
{
    public const string SignInRoute = "/sign/in/two-factor";
    public const string ProfileRoute = "/profile/two-factor";

    extension(IEndpointRouteBuilder application)
    {
        public void MapTwoFactorEndpoints()
        {
            application.MapPost($"{SignInRoute}/totp", TwoFactorSignInEndpoints.VerifyTotp)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.CodeRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/recovery", TwoFactorSignInEndpoints.VerifyRecoveryCode)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.CodeRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/passkey/options", TwoFactorSignInEndpoints.PasskeyOptions)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.TicketRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<PasskeyAssertionChallenge>();

            application.MapPost($"{SignInRoute}/passkey", TwoFactorSignInEndpoints.VerifyPasskey)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.PasskeyRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/setup/totp", TwoFactorSignInEndpoints.SetupTotp)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.TicketRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<TotpSetup>();

            application.MapPost($"{SignInRoute}/setup/totp/confirm", TwoFactorSignInEndpoints.ConfirmTotp)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.CodeRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/setup/passkey/options", TwoFactorSignInEndpoints.SetupPasskeyOptions)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.TicketRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<PasskeyRegistrationChallenge>();

            application.MapPost($"{SignInRoute}/setup/passkey", TwoFactorSignInEndpoints.SetupPasskey)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.PasskeyRegistrationRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapGet(ProfileRoute, TwoFactorProfileEndpoints.GetStatus)
                .ProfileStep<TwoFactorProfileEndpoints.StatusResponse>();

            application.MapPost($"{ProfileRoute}/totp", TwoFactorProfileEndpoints.BeginTotp)
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .ProfileStep<TotpSetup>();

            application.MapPost($"{ProfileRoute}/totp/confirm", TwoFactorProfileEndpoints.ConfirmTotp)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorProfileEndpoints.CodeRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .ProfileStep<Enrolled>();

            application.MapDelete($"{ProfileRoute}/totp", TwoFactorProfileEndpoints.RemoveTotp)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapPost($"{ProfileRoute}/passkeys/options", TwoFactorProfileEndpoints.PasskeyOptions)
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .ProfileStep<PasskeyRegistrationChallenge>();

            application.MapPost($"{ProfileRoute}/passkeys", TwoFactorProfileEndpoints.AddPasskey)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorProfileEndpoints.PasskeyRegistrationRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .ProfileStep<Enrolled>();

            application.MapDelete($"{ProfileRoute}/passkeys/{{id:guid}}", TwoFactorProfileEndpoints.RemovePasskey)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapPost($"{ProfileRoute}/recovery-codes", TwoFactorProfileEndpoints.RegenerateRecoveryCodes)
                .ProfileStep<Enrolled>();
        }
    }

    extension(RouteHandlerBuilder builder)
    {
        private RouteHandlerBuilder SignInStep<TResponse>() =>
            builder
                .Produces<TResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(429)
                .Produces(500);

        private RouteHandlerBuilder ProfileStep<TResponse>() =>
            builder
                .Produces<TResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(409)
                .Produces(429)
                .Produces(500);
    }
}
