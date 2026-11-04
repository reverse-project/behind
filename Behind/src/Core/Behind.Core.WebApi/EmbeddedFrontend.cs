using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Behind.Core.WebApi;

public static class EmbeddedFrontend
{
    public const string Root = "Frontend";

    public static IFileProvider CreateFileProvider() =>
        new ManifestEmbeddedFileProvider(typeof(ICoreWebApi).Assembly, Root);

    public static IFileProvider CreateFileProvider(string? webRootPath) =>
        CreateFileProvider().WithWebRoot(webRootPath);
}
