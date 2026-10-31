using Microsoft.Extensions.FileProviders;

namespace Behind.Core.WebApi;

public static class EmbeddedFrontend
{
    public const string Root = "Frontend";

    public static IFileProvider CreateFileProvider() =>
        new ManifestEmbeddedFileProvider(typeof(ICoreWebApi).Assembly, Root);
}
