using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class CommentsConfigurationTests
{
    private static CommentsConfiguration Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(CommentsConfiguration.SectionName)
            .Get<CommentsConfiguration>() ?? new CommentsConfiguration();

    [Fact]
    public void SectionName_IsBackendComments()
    {
        Assert.Equal("Backend:Comments", CommentsConfiguration.SectionName);
    }

    [Fact]
    public void Defaults_EnableTheCommentsOfTheNews()
    {
        Assert.True(new CommentsConfiguration().IsEnabled(CommentsConfiguration.NewsEntity));
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void Binding_ReadsTheSwitchOfTheNews(string value, bool expected)
    {
        Assert.Equal(expected, Bind(new() { ["Backend:Comments:News"] = value }).IsEnabled("News"));
    }

    [Fact]
    public void Binding_MissingSection_KeepsTheDefault()
    {
        Assert.True(Bind(new()).IsEnabled("News"));
    }

    [Fact]
    public void IsEnabled_UnknownEntity_IsOff()
    {
        Assert.False(new CommentsConfiguration().IsEnabled("Unknown"));
    }
}
