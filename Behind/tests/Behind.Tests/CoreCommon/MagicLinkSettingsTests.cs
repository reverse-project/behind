using Behind.Core.Common.Configuration;

namespace Behind.Tests.CoreCommon;

public class MagicLinkSettingsTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(10);
    private static readonly TimeSpan Overlap = TimeSpan.FromDays(2);

    private static MagicLinkSettings SettingsCreatedAt(DateTimeOffset createdAt, Option<Uuid> old = default) =>
        new(Uuid.CreateVersion7(createdAt), old, Some(Interval), Some(Overlap), Some(TimeSpan.FromMinutes(5)));

    [Fact]
    public void Uuid_CreateVersion7_CarriesTimestamp()
    {
        var uuid = Uuid.CreateVersion7(Start);

        Assert.Equal(Some(Start), uuid.CreatedAt);
    }

    [Fact]
    public void Uuid_NonVersion7_HasNoTimestamp()
    {
        Assert.True(Uuid.TryCreate(Guid.NewGuid().ToString(), out var uuid));

        Assert.Equal(None, uuid.CreatedAt);
    }

    [Fact]
    public void Uuid_ToConfigurationValue_UsesUuidScheme()
    {
        var uuid = Uuid.CreateVersion7(Start);

        Assert.Equal($"uuid://{uuid.Value}", uuid.ToConfigurationValue());
    }

    [Fact]
    public void Effective_WithoutConfiguredValues_UsesDefaults()
    {
        var settings = new MagicLinkSettings(Uuid.CreateVersion7(Start), None);

        Assert.Equal(MagicLinkSettings.DefaultRotationInterval, settings.EffectiveRotationInterval);
        Assert.Equal(MagicLinkSettings.DefaultRotationOverlap, settings.EffectiveRotationOverlap);
        Assert.Equal(MagicLinkSettings.DefaultLinkLifetime, settings.EffectiveLinkLifetime);
    }

    [Fact]
    public void Effective_WithConfiguredValues_UsesConfigured()
    {
        var settings = SettingsCreatedAt(Start);

        Assert.Equal(Interval, settings.EffectiveRotationInterval);
        Assert.Equal(Overlap, settings.EffectiveRotationOverlap);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.EffectiveLinkLifetime);
    }

    [Fact]
    public void Effective_RequestProtection_DefaultsAndOverrides()
    {
        var defaults = new MagicLinkSettings(Uuid.CreateVersion7(Start), None);
        var configured = defaults with
        {
            RequestCooldown = Some(TimeSpan.FromSeconds(30)),
            RequestLimit = Some(3),
            RequestWindow = Some(TimeSpan.FromMinutes(1)),
            RedeemLimit = Some(7),
            RedeemWindow = Some(TimeSpan.FromMinutes(2))
        };

        Assert.Equal(MagicLinkSettings.DefaultRequestCooldown, defaults.EffectiveRequestCooldown);
        Assert.Equal(MagicLinkSettings.DefaultRequestLimit, defaults.EffectiveRequestLimit);
        Assert.Equal(MagicLinkSettings.DefaultRequestWindow, defaults.EffectiveRequestWindow);
        Assert.Equal(TimeSpan.FromSeconds(30), configured.EffectiveRequestCooldown);
        Assert.Equal(MagicLinkSettings.DefaultRedeemLimit, defaults.EffectiveRedeemLimit);
        Assert.Equal(MagicLinkSettings.DefaultRedeemWindow, defaults.EffectiveRedeemWindow);
        Assert.Equal(7, configured.EffectiveRedeemLimit);
        Assert.Equal(TimeSpan.FromMinutes(2), configured.EffectiveRedeemWindow);
        Assert.Equal(3, configured.EffectiveRequestLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), configured.EffectiveRequestWindow);
    }

    [Fact]
    public void IsRotationDue_BeforeInterval_IsFalse() =>
        Assert.False(SettingsCreatedAt(Start).IsRotationDue(Start + Interval - TimeSpan.FromSeconds(1)));

    [Fact]
    public void IsRotationDue_AtInterval_IsTrue() =>
        Assert.True(SettingsCreatedAt(Start).IsRotationDue(Start + Interval));

    [Fact]
    public void IsRotationDue_SecretWithoutTimestamp_IsTrue()
    {
        Uuid.TryCreate(Guid.NewGuid().ToString(), out var uuid);

        Assert.True(new MagicLinkSettings(uuid, None).IsRotationDue(Start));
    }

    [Fact]
    public void Rotate_PromotesActualToOldAndCreatesNewActual()
    {
        var settings = SettingsCreatedAt(Start);
        var now = Start + Interval;

        var rotated = settings.Rotate(now);

        Assert.Equal(Some(settings.ActualSecret), rotated.OldSecret);
        Assert.NotEqual(settings.ActualSecret, rotated.ActualSecret);
        Assert.Equal(Some(now), rotated.ActualSecret.CreatedAt);
        Assert.Equal(settings.RotationInterval, rotated.RotationInterval);
        Assert.Equal(settings.RotationOverlap, rotated.RotationOverlap);
    }

    [Fact]
    public void ValidSecrets_WithinOverlap_ContainsActualAndOld()
    {
        var old = Uuid.CreateVersion7(Start - Interval);
        var settings = SettingsCreatedAt(Start, Some(old));

        var secrets = settings.ValidSecrets(Start + Overlap - TimeSpan.FromSeconds(1));

        Assert.Equal([settings.ActualSecret, old], secrets);
    }

    [Fact]
    public void ValidSecrets_AfterOverlap_ContainsOnlyActual()
    {
        var old = Uuid.CreateVersion7(Start - Interval);
        var settings = SettingsCreatedAt(Start, Some(old));

        Assert.Equal([settings.ActualSecret], settings.ValidSecrets(Start + Overlap));
    }

    [Fact]
    public void ValidSecrets_WithoutOld_ContainsOnlyActual()
    {
        var settings = SettingsCreatedAt(Start);

        Assert.Equal([settings.ActualSecret], settings.ValidSecrets(Start));
    }

    [Fact]
    public void Advance_RotationDue_Rotates()
    {
        var settings = SettingsCreatedAt(Start);

        var advanced = settings.Advance(Start + Interval);

        Assert.Equal(Some(settings.ActualSecret), advanced.OldSecret);
    }

    [Fact]
    public void Advance_OverlapElapsed_DropsOldSecret()
    {
        var settings = SettingsCreatedAt(Start, Some(Uuid.CreateVersion7(Start - Interval)));

        var advanced = settings.Advance(Start + Overlap);

        Assert.Equal(None, advanced.OldSecret);
        Assert.Equal(settings.ActualSecret, advanced.ActualSecret);
    }

    [Fact]
    public void Advance_NothingToDo_ReturnsEqualSettings()
    {
        var settings = SettingsCreatedAt(Start, Some(Uuid.CreateVersion7(Start - Interval)));

        Assert.Equal(settings, settings.Advance(Start + TimeSpan.FromHours(1)));
    }
}
