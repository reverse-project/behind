using Behind.Core.WebApi.Endpoints;
using Behind.Common.WebApi.Infrastructure.Localization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class LocaleEndpointsTests
{
    [Fact]
    public async Task Get_ListsTheAvailableLocalesByTheirOfficialNames()
    {
        var result = await LocaleEndpoints.Get(TestLocales.Embedded, CancellationToken.None);

        var locales = Assert.IsType<JsonHttpResult<IReadOnlyList<LocaleInfo>>>(result).Value!;
        Assert.Contains(new LocaleInfo("en-US", "English (International)"), locales);
        Assert.Contains(new LocaleInfo("hu-HU", "Magyar"), locales);
    }
}
