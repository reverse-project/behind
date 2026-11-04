using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

    extension(HttpContext context)
    {
        private Option<MagicLinkSettings> CurrentMagicLinkSettings =>
            context.RequestServices.GetRequiredService<IMagicLinkSettingsStore>().Current;
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddMagicLink(FileInfo securityFile)
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IMagicLinkSettingsStore>(provider =>
            {
                var security = provider.GetRequiredService<IOptionsMonitor<SecurityConfiguration>>();
                var store = new MagicLinkSettingsStore(security.CurrentValue.MagicLink);
                security.OnChange(configuration => store.Set(configuration.MagicLink));

                return store;
            });
            services.AddSingleton<IMagicLinkSettingsPersister>(provider =>
                new ConfigurationAwareMagicLinkSettingsPersister(
                    provider.GetRequiredService<IOptions<SecurityConfiguration>>(),
                    securityFile));
            services.AddSingleton<MagicLinkSecretRotator>();
            services.AddHostedService<MagicLinkSecretRotationService>();
            services.AddSingleton<IMagicLinkMailQueue, MagicLinkMailQueue>();
            services.AddHostedService<MagicLinkMailService>();
            services.AddHostedService<MagicLinkCleanupService>();

            return services.AddMagicLinkRateLimiting();
        }

        public IServiceCollection AddMagicLinkRateLimiting() =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddClientPolicy(
                    RequestRateLimitPolicy,
                    context => context.CurrentMagicLinkSettings.Map(settings =>
                            (settings.EffectiveRequestLimit, settings.EffectiveRequestWindow))
                        .GetOrElse((MagicLinkSettings.DefaultRequestLimit, MagicLinkSettings.DefaultRequestWindow)));
                options.AddClientPolicy(
                    RedeemRateLimitPolicy,
                    context => context.CurrentMagicLinkSettings.Map(settings =>
                            (settings.EffectiveRedeemLimit, settings.EffectiveRedeemWindow))
                        .GetOrElse((MagicLinkSettings.DefaultRedeemLimit, MagicLinkSettings.DefaultRedeemWindow)));
            });
    }
}
