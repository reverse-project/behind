using NUglify;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public sealed record CssMinifier()
    : NUglifyContentMinifier(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".css" })
{
    protected override UglifyResult Run(string content) => Uglify.Css(content);
}
