using NUglify;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public sealed record HtmlMinifier()
    : NUglifyContentMinifier(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".html", ".htm" })
{
    protected override UglifyResult Run(string content) => Uglify.Html(content);
}
