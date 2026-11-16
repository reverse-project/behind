using System.Text.Json;
using Behind.Common.Modules;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Layouts;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi;

public sealed class LayoutBadgeTests
{
    private static LayoutConfiguration Configuration(params LayoutBadge[] badges) => new()
    {
        Layouts = [new SiteLayout { Badges = badges }]
    };

    [Theory]
    [InlineData("")]
    [InlineData("#/funding")]
    [InlineData("/funding")]
    [InlineData("https://example.org/funding")]
    [InlineData("mailto:funding@example.org")]
    public void TextBadges_AllowSafeOptionalLinks(string link)
    {
        Assert.True(Configuration(new LayoutBadge { Text = "Under development", Link = link }).IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("//example.org")]
    [InlineData("/\\example.org")]
    [InlineData("/\n/example.org")]
    [InlineData(" https://example.org")]
    public void UnsafeBadgeLinks_AreRejected(string link)
    {
        Assert.False(Configuration(new LayoutBadge { Text = "Funding", Link = link }).IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData("top-left")]
    [InlineData("top-right")]
    [InlineData("bottom-left")]
    [InlineData("bottom-right")]
    public void ImageBadges_RequireAccessibleTextAndLayoutVfsImage(string position)
    {
        Assert.True(Configuration(new LayoutBadge
        {
            Text = "European Union funding", Image = $"/layouts/images/{Guid.NewGuid():D}", Position = position
        }).IsValid(ModuleCatalog.Empty));
    }

    [Theory]
    [InlineData("https://example.org/image.png")]
    [InlineData("/sites/images/0a1bc242-6328-4021-959c-96793c556da7")]
    [InlineData("/layouts/images/not-a-guid")]
    [InlineData("/layouts/images/00000000-0000-0000-0000-000000000000")]
    [InlineData("/layouts/images/0a1bc242-6328-4021-959c-96793c556da7?x=1")]
    public void InvalidImages_AreRejected(string image)
    {
        Assert.False(Configuration(new LayoutBadge { Text = "Funding", Image = image }).IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public void InvalidBadgeCollectionsAndFields_AreRejected()
    {
        var valid = new LayoutBadge { Id = "funding", Text = "Funding" };
        foreach (var invalid in new[]
        {
            valid with { Id = "" }, valid with { Text = "" }, valid with { Text = " " },
            valid with { Text = new string('a', 201) }, valid with { Image = null! },
            valid with { Link = null! }, valid with { Position = "fixed" },
            valid with { Width = 39 }, valid with { Width = 601 }
        })
        {
            Assert.False(Configuration(invalid).IsValid(ModuleCatalog.Empty));
        }
        Assert.False(Configuration(null!).IsValid(ModuleCatalog.Empty));
        Assert.False(Configuration([null!]).IsValid(ModuleCatalog.Empty));
        Assert.False(Configuration(valid, valid).IsValid(ModuleCatalog.Empty));
        Assert.True(Configuration(Enumerable.Range(0, 20).Select(index => valid with { Id = index.ToString() }).ToArray())
            .IsValid(ModuleCatalog.Empty));
        Assert.False(Configuration(Enumerable.Range(0, 21).Select(index => valid with { Id = index.ToString() }).ToArray())
            .IsValid(ModuleCatalog.Empty));
        Assert.True(Configuration(valid with { Width = 40 }, valid with { Id = "second", Width = 600 })
            .IsValid(ModuleCatalog.Empty));
    }

    [Fact]
    public async Task Badges_PersistAndCanBeRemovedWithoutChangingContent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"behind-badges-{Guid.NewGuid():N}");
        try
        {
            using var service = new LayoutService(Options.Create(new BackendConfiguration { DataDirectory = directory }), ModuleCatalog.Empty);
            var badge = new LayoutBadge
            {
                Id = "funding", Text = "<b>EU funding</b>", Image = $"/layouts/images/{Guid.NewGuid():D}",
                Link = "https://example.org/funding", Position = "bottom-right", Width = 250
            };
            Assert.Null((await service.Save(Configuration(badge), false, CancellationToken.None)).Error);
            var loaded = await service.Read(CancellationToken.None);
            Assert.Equal(badge, Assert.Single(loaded.Layouts[0].Badges));
            Assert.Equal("core.content", Assert.Single(loaded.Layouts[0].Sections[0].Widgets).Type);
            var empty = loaded with { Layouts = [loaded.Layouts[0] with { Badges = [] }] };
            Assert.Null((await service.Save(empty, false, CancellationToken.None)).Error);
            Assert.Empty((await service.Read(CancellationToken.None)).Layouts[0].Badges);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void OlderLayouts_DefaultToNoBadges()
    {
        var configuration = JsonSerializer.Deserialize<LayoutConfiguration>(
            """{"layouts":[{"id":"default","name":"Default"}]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Empty(configuration.Layouts[0].Badges);
        Assert.True(configuration.IsValid(ModuleCatalog.Empty));
    }
}
