namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public interface IContentMinifier
{
    IReadOnlySet<string> Extensions { get; }

    Option<string> Minify(string content);
}
