using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CommonWebApi.StaticFiles;

public class FrontendOptionsTests
{
    private static FrontendOptions? Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(FrontendOptions.SectionName)
            .Get<FrontendOptions>();

    [Fact]
    public void Default_EnablesMinificationAndCompressionWithIndexHtml()
    {
        Assert.True(FrontendOptions.Default.Minify);
        Assert.True(FrontendOptions.Default.Compress);
        Assert.Equal("index.html", FrontendOptions.Default.DefaultFile);
    }

    [Fact]
    public void Bind_OverridesConfiguredValuesAndKeepsDefaults()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Frontend:StaticFiles:Minify"] = "false",
            ["Frontend:StaticFiles:DefaultFile"] = "home.html"
        });

        Assert.NotNull(options);
        Assert.False(options.Minify);
        Assert.True(options.Compress);
        Assert.Equal("home.html", options.DefaultFile);
        Assert.Equal(FrontendOptions.Default.CompressedMimeTypes, options.CompressedMimeTypes);
    }

    [Fact]
    public void Bind_MissingSection_ReturnsNull()
    {
        Assert.Null(Bind(new Dictionary<string, string?>()));
    }
}
