using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Behind.Common.Configuration;
using Behind.Common.Converters;

namespace Behind.Tests.CoreCommon;

public class MagicLinkSettingsBindingTests
{
    private static MagicLinkSettings Bind(Dictionary<string, string?> values)
    {
        OptionTypeConverterRegistration.Register<Uuid>();
        OptionTypeConverterRegistration.Register<MagicLinkSettings>();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return configuration.GetSection("MagicLink").GetOptionAware<MagicLinkSettings>()!;
    }

    [Fact]
    public void Bind_OnlyActualSecret_UsesDefaults()
    {
        var uuid = Uuid.CreateVersion7(DateTimeOffset.UtcNow);

        var settings = Bind(new() { ["MagicLink:ActualSecret"] = uuid.ToConfigurationValue() });

        Assert.Equal(uuid, settings.ActualSecret);
        Assert.Equal(None, settings.OldSecret);
        Assert.Equal(MagicLinkSettings.DefaultRotationInterval, settings.EffectiveRotationInterval);
        Assert.Equal(MagicLinkSettings.DefaultRotationOverlap, settings.EffectiveRotationOverlap);
    }

    [Fact]
    public void Bind_AllValues_AreApplied()
    {
        var actual = Uuid.CreateVersion7(DateTimeOffset.UtcNow);
        var old = Uuid.CreateVersion7(DateTimeOffset.UtcNow.AddDays(-1));

        var settings = Bind(new()
        {
            ["MagicLink:ActualSecret"] = actual.ToConfigurationValue(),
            ["MagicLink:OldSecret"] = old.ToConfigurationValue(),
            ["MagicLink:RotationInterval"] = "7.00:00:00",
            ["MagicLink:RotationOverlap"] = "02:00:00",
            ["MagicLink:LinkLifetime"] = "00:10:00"
        });

        Assert.Equal(Some(old), settings.OldSecret);
        Assert.Equal(TimeSpan.FromDays(7), settings.EffectiveRotationInterval);
        Assert.Equal(TimeSpan.FromHours(2), settings.EffectiveRotationOverlap);
        Assert.Equal(TimeSpan.FromMinutes(10), settings.EffectiveLinkLifetime);
    }
}
