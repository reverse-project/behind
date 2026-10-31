using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Common.Configuration;

public static class MagicLinkSettingsExtensions
{
    extension(MagicLinkSettings settings)
    {
        public TimeSpan EffectiveRotationInterval =>
            settings.RotationInterval.GetOrElse(MagicLinkSettings.DefaultRotationInterval);

        public TimeSpan EffectiveRotationOverlap =>
            settings.RotationOverlap.GetOrElse(MagicLinkSettings.DefaultRotationOverlap);

        public TimeSpan EffectiveLinkLifetime =>
            settings.LinkLifetime.GetOrElse(MagicLinkSettings.DefaultLinkLifetime);

        public TimeSpan EffectiveRequestCooldown =>
            settings.RequestCooldown.GetOrElse(MagicLinkSettings.DefaultRequestCooldown);

        public int EffectiveRequestLimit =>
            settings.RequestLimit.GetOrElse(MagicLinkSettings.DefaultRequestLimit);

        public TimeSpan EffectiveRequestWindow =>
            settings.RequestWindow.GetOrElse(MagicLinkSettings.DefaultRequestWindow);

        public int EffectiveRedeemLimit =>
            settings.RedeemLimit.GetOrElse(MagicLinkSettings.DefaultRedeemLimit);

        public TimeSpan EffectiveRedeemWindow =>
            settings.RedeemWindow.GetOrElse(MagicLinkSettings.DefaultRedeemWindow);

        public bool IsRotationDue(DateTimeOffset now) =>
            settings.ActualSecret.CreatedAt
                .Map(createdAt => now >= createdAt + settings.EffectiveRotationInterval)
                .GetOrElse(true);

        public bool IsOverlapActive(DateTimeOffset now) =>
            settings.ActualSecret.CreatedAt
                .Map(createdAt => now < createdAt + settings.EffectiveRotationOverlap)
                .GetOrElse(true);

        public IReadOnlyList<Uuid> ValidSecrets(DateTimeOffset now) =>
        [
            settings.ActualSecret,
            ..settings.OldSecret.AsEnumerable().Where(_ => settings.IsOverlapActive(now))
        ];

        public MagicLinkSettings Rotate(DateTimeOffset now) =>
            settings with
            {
                ActualSecret = Uuid.CreateVersion7(now),
                OldSecret = Some(settings.ActualSecret)
            };

        public MagicLinkSettings Advance(DateTimeOffset now) =>
            settings switch
            {
                _ when settings.IsRotationDue(now) => settings.Rotate(now),
                _ when settings.OldSecret != None && !settings.IsOverlapActive(now) =>
                    settings with { OldSecret = None },
                _ => settings
            };
    }
}
