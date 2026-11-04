using Behind.Common.WebApi.Infrastructure.Localization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class LocaleEndpoints
{
    public static async Task<IResult> Get(LocaleCatalog locales, CancellationToken cancellationToken) =>
        Results.Json(await locales.ListAsync(cancellationToken));
}
