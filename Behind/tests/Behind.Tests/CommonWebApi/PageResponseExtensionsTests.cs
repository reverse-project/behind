using Behind.Common.WebApi;

namespace Behind.Tests.CommonWebApi;

public class PageResponseExtensionsTests
{
    [Fact]
    public void ToPageResponse_MapsEveryItemAndKeepsThePaging()
    {
        var result = new PagedResult<int>(25, 2, 3, 10, Some<IEnumerable<int>>([1, 2, 3]));

        var page = result.ToPageResponse(value => $"#{value}");

        Assert.Equal((25L, 2L, 3L, 10), (page.Total, page.Page, page.PageCount, page.PageSize));
        Assert.Equal(["#1", "#2", "#3"], page.Items);
    }

    [Fact]
    public void ToPageResponse_WithoutResults_HasNoItems()
    {
        var result = new PagedResult<int>(0, 1, 0, 10, None);

        Assert.Empty(result.ToPageResponse(value => value).Items);
    }
}
