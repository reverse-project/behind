namespace Behind.Tests.Domain;

public class SiteTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewSite_IsAnUnpublishedDraftWithoutAnIdentity()
    {
        var site = new Site("About", "about", "<p/>", Now);

        Assert.Equal(new VersionedKey<Guid>(Guid.Empty), site.Id);
        Assert.Equal(None, site.PublishedAt);
        Assert.True(site.IsDraft);
        Assert.Equal(Now, site.CreatedAt);
        Assert.False(site.HideTitle);
    }

    [Fact]
    public void NewSite_CanHideItsTitle()
    {
        Assert.True(new Site("Home", "home", "<p/>", Now, true).HideTitle);
    }

    [Fact]
    public void WithId_ReplacesTheVersionedKey()
    {
        var id = new VersionedKey<Guid>(Guid.NewGuid(), 3);

        Assert.Equal(id, new Site("About", "about", "<p/>", Now).WithId(id).Id);
    }

    [Fact]
    public void WithPublishedAt_MakesTheVersionPublished()
    {
        var site = new Site("About", "about", "<p/>", Now).WithPublishedAt(Some(Now));

        Assert.False(site.IsDraft);
        Assert.Equal(Some(Now), site.PublishedAt);
    }

    [Fact]
    public void EntityType_IsTheNameUsedByTheActiveVersion()
    {
        Assert.Equal("Site", Site.EntityType);
    }

    [Fact]
    public void SiteGrantArea_IsPartOfTheBuiltInAreas()
    {
        Assert.Contains(GrantArea.Site, GrantArea.All);
        Assert.Contains("Site.Write", BuiltInAccess.GrantNames);
    }
}

public class SiteImageExtensionsTests
{
    public static TheoryData<byte[], string> Images => new()
    {
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], "image/png" },
        { [0xFF, 0xD8, 0xFF, 0xE0], "image/jpeg" },
        { "GIF89a...."u8.ToArray(), "image/gif" },
        { "GIF87a...."u8.ToArray(), "image/gif" },
        { "RIFF\0\0\0\0WEBPVP8 "u8.ToArray(), "image/webp" }
    };

    [Theory]
    [MemberData(nameof(Images))]
    public void DetectImageContentType_RecognisesTheSupportedFormats(byte[] content, string expected)
    {
        Assert.Equal(Some(expected), content.DetectImageContentType());
    }

    [Theory]
    [InlineData("")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'/>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("RIFF\0\0\0\0WAVEfmt ")]
    [InlineData("RIFF")]
    public void DetectImageContentType_RejectsEverythingElse(string text)
    {
        Assert.Equal(None, System.Text.Encoding.UTF8.GetBytes(text).DetectImageContentType());
    }
}
