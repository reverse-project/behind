using NUglify;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public sealed record JavaScriptMinifier()
    : NUglifyContentMinifier(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".js", ".mjs" })
{
    protected override UglifyResult Run(string content) => Uglify.Js(content);
}
