using NUglify;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public abstract record NUglifyContentMinifier(IReadOnlySet<string> Extensions) : IContentMinifier
{
    protected abstract UglifyResult Run(string content);

    public Option<string> Minify(string content)
    {
        var result = Run(content);

        return result.HasErrors || string.IsNullOrWhiteSpace(result.Code)
            ? None
            : Some(result.Code);
    }
}
