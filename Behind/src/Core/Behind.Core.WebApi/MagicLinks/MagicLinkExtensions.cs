using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Behind.Core.WebApi.MagicLinks;

public static class MagicLinkExtensions
{
    public const string SignInRoute = "/sign/in/magiclink";
    public const string TokenParameter = "token";
    public const string RequestRateLimitPolicy = "magic-link-request";
    public const string RedeemRateLimitPolicy = "magic-link-redeem";

    extension(MagicLinkCredential credential)
    {
        public string ToLink(Uri baseUri) =>
            baseUri.ToFrontendLink(SignInRoute, [new(TokenParameter, credential.Token)]);
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddMagicLink(SecurityConfiguration security, FileInfo securityFile)
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IMagicLinkSettingsStore>(new MagicLinkSettingsStore(security.MagicLink));
            services.AddSingleton<IMagicLinkSettingsPersister>(
                security.ReadOnlyConfiguration || !securityFile.Exists
                    ? new NoOpMagicLinkSettingsPersister()
                    : new JsonFileMagicLinkSettingsPersister(securityFile));
            services.AddSingleton<MagicLinkSecretRotator>();
            services.AddHostedService<MagicLinkSecretRotationService>();
            services.AddSingleton<IMagicLinkMailQueue, MagicLinkMailQueue>();
            services.AddHostedService<MagicLinkMailService>();

            return security.MagicLink.Match(
                none: () => services,
                some: settings => services
                    .AddHostedService<MagicLinkCleanupService>()
                    .AddMagicLinkRateLimiting(settings));
        }

        public IServiceCollection AddMagicLinkRateLimiting(MagicLinkSettings settings) =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddClientPolicy(
                    RequestRateLimitPolicy,
                    settings.EffectiveRequestLimit,
                    settings.EffectiveRequestWindow);
                options.AddClientPolicy(
                    RedeemRateLimitPolicy,
                    settings.EffectiveRedeemLimit,
                    settings.EffectiveRedeemWindow);
            });
    }
}
