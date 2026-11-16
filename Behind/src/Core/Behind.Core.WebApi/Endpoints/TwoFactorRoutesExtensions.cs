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
            application.MapPost($"{SignInRoute}/email/addresses", EmailTwoFactorEndpoints.Addresses)
                .RequireAccountMode(AccountMode.EmailTwoFactor)
                .AddEndpointFilter<DataAnnotationValidationFilter<EmailTwoFactorEndpoints.AddressRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<EmailTwoFactorEndpoints.AddressesResponse>();

            application.MapPost($"{SignInRoute}/email", EmailTwoFactorEndpoints.Send)
                .RequireAccountMode(AccountMode.EmailTwoFactor)
                .AddEndpointFilter<DataAnnotationValidationFilter<EmailTwoFactorEndpoints.SendRequest>>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<EmailTwoFactorEndpoints.DeliveryResponse>()
                .Produces(503);

            application.MapPost($"{SignInRoute}/email/verify", EmailTwoFactorEndpoints.Verify)
                .RequireAccountMode(AccountMode.EmailTwoFactor)
                .AddEndpointFilter<DataAnnotationValidationFilter<EmailTwoFactorEndpoints.VerifyRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/totp", TwoFactorSignInEndpoints.VerifyTotp)
                .RequireAccountMode(AccountMode.TwoFactor)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.CodeRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/recovery", TwoFactorSignInEndpoints.VerifyRecoveryCode)
                .RequireAccountMode(AccountMode.TwoFactor)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.CodeRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/passkey/options", TwoFactorSignInEndpoints.PasskeyOptions)
                .RequireAccountMode(AccountMode.Passkey)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.TicketRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<PasskeyAssertionChallenge>();

            application.MapPost($"{SignInRoute}/passkey", TwoFactorSignInEndpoints.VerifyPasskey)
                .RequireAccountMode(AccountMode.Passkey)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.PasskeyRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/setup/totp", TwoFactorSignInEndpoints.SetupTotp)
                .RequireAccountMode(AccountMode.TwoFactor)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.TicketRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<TotpSetup>();

            application.MapPost($"{SignInRoute}/setup/totp/confirm", TwoFactorSignInEndpoints.ConfirmTotp)
                .RequireAccountMode(AccountMode.TwoFactor)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.CodeRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapPost($"{SignInRoute}/setup/passkey/options", TwoFactorSignInEndpoints.SetupPasskeyOptions)
                .RequireAccountMode(AccountMode.Passkey)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.TicketRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<PasskeyRegistrationChallenge>();

            application.MapPost($"{SignInRoute}/setup/passkey", TwoFactorSignInEndpoints.SetupPasskey)
                .RequireAccountMode(AccountMode.Passkey)
                .AddEndpointFilter<DataAnnotationValidationFilter<TwoFactorSignInEndpoints.PasskeyRegistrationRequest>>()
                .AddEndpointFilter<TwoFactorChallengeFilter>()
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .SignInStep<SignInEndpoints.SignInResponse>();

            application.MapGet(ProfileRoute, TwoFactorProfileEndpoints.GetStatus)
                .ProfileStep<TwoFactorProfileEndpoints.StatusResponse>();

            application.MapPost($"{ProfileRoute}/totp", TwoFactorProfileEndpoints.BeginTotp)
                .RequireAccountMode(AccountMode.TwoFactor)
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .ProfileStep<TotpSetup>();

            application.MapPost($"{ProfileRoute}/totp/confirm", TwoFactorProfileEndpoints.ConfirmTotp)
                .RequireAccountMode(AccountMode.TwoFactor)
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
                .RequireAccountMode(AccountMode.Passkey)
                .RequireRateLimiting(TwoFactorExtensions.RateLimitPolicy)
                .ProfileStep<PasskeyRegistrationChallenge>();

            application.MapPost($"{ProfileRoute}/passkeys", TwoFactorProfileEndpoints.AddPasskey)
                .RequireAccountMode(AccountMode.Passkey)
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
