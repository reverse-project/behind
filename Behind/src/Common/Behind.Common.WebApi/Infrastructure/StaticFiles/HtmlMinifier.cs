using NUglify;
using NUglify.Html;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public sealed record HtmlMinifier()
    : NUglifyContentMinifier(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".html", ".htm" })
{
    private static readonly HtmlSettings Settings = new() { RemoveOptionalTags = false };

    protected override UglifyResult Run(string content) => Uglify.Html(content, Settings);
}
