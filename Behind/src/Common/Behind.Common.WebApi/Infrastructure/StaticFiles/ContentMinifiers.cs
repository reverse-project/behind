using System.Collections.Immutable;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public static class ContentMinifiers
{
    public static readonly IReadOnlyList<IContentMinifier> Default =
        ImmutableArray.Create<IContentMinifier>(new JavaScriptMinifier(), new CssMinifier(), new HtmlMinifier());
}
