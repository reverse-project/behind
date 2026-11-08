namespace Behind.Tests.Common;

public class SportSupportExtensionsTests
{
    [Theory]
    [InlineData("  a b ", "a b")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void ToRequiredText_TrimsAndNeverReturnsNull(string? value, string expected)
    {
        Assert.Equal(expected, value.ToRequiredText());
    }

    [Theory]
    [InlineData("  a b ", "a b")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void ToOptionalText_TrimsAndTurnsBlankIntoNull(string? value, string? expected)
    {
        Assert.Equal(expected, value.ToOptionalText());
    }

    [Fact]
    public void ToPage_CarriesTheFlipperAndOnlyHasResultsWhenThereAreRows()
    {
        var flipper = new Flipper(2, 5);

        var page = flipper.ToPage(12, ["a", "b"]);
        var empty = flipper.ToPage<string>(0, []);

        Assert.Equal((12L, 2L, 3L, 5), (page.Total, page.Page, page.PageCount, page.PageSize));
        Assert.Equal(["a", "b"], page.Results.Match(() => [], items => items.ToList()));
        Assert.True(empty.Results.IsNone());
    }

    [Fact]
    public async Task MapAsync_MapsSomeAndKeepsNone()
    {
        Assert.Equal(Some(4), await Some(2).MapAsync(value => Task.FromResult(value * 2)));
        Assert.True((await ((Option<int>)None).MapAsync(value => Task.FromResult(value * 2))).IsNone());
        Assert.Equal(Some(3), await Task.FromResult<Option<int>>(Some(1)).MapAsync(value => Task.FromResult(value + 2)));
    }

    [Fact]
    public void SameKindAs_GroupsAnUnknownModuleTypeByItself()
    {
        Assert.Equal(["SportTeams"], VfsTypes.SameKindAs("SportTeams"));
        Assert.Equal(VfsTypes.AllSiteImages, VfsTypes.SameKindAs(VfsTypes.News));
    }
}
