using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Behind.Core.WebApi.Endpoints;

public static class AboutEndpoints
{
    public static IResult Get(IConfiguration configuration) =>
        Results.Json(
            AboutResponse.Current with
            {
                Title = configuration.SiteSetting(nameof(Site.Title)) is { Length: > 0 } title
                    ? title
                    : AboutResponse.ProductName,
                Copyright = configuration.SiteSetting(nameof(Site.Copyright)) ?? string.Empty
            }
        );

    extension(IConfiguration configuration)
    {
        private string? SiteSetting(string name) =>
            configuration
                .GetSection(FrontendConfiguration.SectionName)
                .GetSection(nameof(FrontendConfiguration.Site))
                .GetValue<string>(name);
    }
}
