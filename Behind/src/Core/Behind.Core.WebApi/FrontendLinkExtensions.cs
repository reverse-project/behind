using Behind.Common;
using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi;

public static class FrontendLinkExtensions
{
    extension(IConfiguration configuration)
    {
        public Option<Uri> FrontendUri() =>
            Uri.TryCreate(configuration[$"{FrontendConfiguration.SectionName}:{nameof(FrontendConfiguration.Url)}"],
                UriKind.Absolute, out var url)
            && url.Scheme is "https" or "http"
            && string.IsNullOrEmpty(url.UserInfo)
            && string.IsNullOrEmpty(url.Query)
            && string.IsNullOrEmpty(url.Fragment)
                ? Some(url)
                : None;
    }

    extension(Uri baseUri)
    {
        public string ToFrontendLink(string route, IEnumerable<KeyValuePair<string, string?>> query) =>
            $"{baseUri.GetLeftPart(UriPartial.Path)}#{QueryHelpers.AddQueryString(route, query)}";
    }
}
