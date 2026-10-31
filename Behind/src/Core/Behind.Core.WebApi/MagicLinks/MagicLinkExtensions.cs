using System.Threading.RateLimiting;
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
    public const string SignInPath = "sign/in/magiclink";
    public const string TokenParameter = "token";
    public const string RequestRateLimitPolicy = "magic-link-request";

    private const string UnknownClient = "unknown";

    extension(MagicLinkCredential credential)
    {
        public string ToLink(Uri baseUri) =>
            QueryHelpers.AddQueryString(new Uri(baseUri, SignInPath).ToString(), TokenParameter, credential.Token);
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
                options.AddPolicy(
                    RequestRateLimitPolicy,
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.EffectiveRequestLimit,
                            Window = settings.EffectiveRequestWindow,
                            QueueLimit = 0
                        }));
            });
    }
}
