using System.Text.Json;

namespace Behind.Tests.Domain;

public class SiteSectionTests
{
    [Fact]
    public void NewSection_HasNoIdentityYet()
    {
        var section = new SiteSection(SiteSectionTheme.Accented, "Hero", "<p/>", "Alt", SiteSectionImagePosition.Right);

        Assert.Equal(new VersionedKey<Guid>(Guid.Empty), section.Id);
        Assert.Equal(SiteSectionTheme.Accented, section.Theme);
        Assert.Equal(SiteSectionImagePosition.Right, section.ImagePosition);
    }

    [Theory]
    [InlineData(SiteSectionTheme.Normal, "\"Normal\"")]
    [InlineData(SiteSectionTheme.Accented, "\"Accented\"")]
    public void Theme_IsWrittenAsItsName(SiteSectionTheme theme, string json)
    {
        Assert.Equal(json, JsonSerializer.Serialize(theme));
        Assert.Equal(theme, JsonSerializer.Deserialize<SiteSectionTheme>(json));
    }

    [Theory]
    [InlineData(SiteSectionImagePosition.Left, "\"Left\"")]
    [InlineData(SiteSectionImagePosition.Right, "\"Right\"")]
    public void ImagePosition_IsWrittenAsItsName(SiteSectionImagePosition position, string json)
    {
        Assert.Equal(json, JsonSerializer.Serialize(position));
        Assert.Equal(position, JsonSerializer.Deserialize<SiteSectionImagePosition>(json));
    }

    [Fact]
    public void Theme_UnknownName_IsRejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SiteSectionTheme>("\"Neon\""));
    }
}
