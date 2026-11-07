using Behind.Common.Modules;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Behind.Core.WebApi;

public static class EmbeddedFrontend
{
    public const string Root = ModuleFrontend.Root;

    public static IFileProvider CreateFileProvider() =>
        new ManifestEmbeddedFileProvider(typeof(ICoreWebApi).Assembly, Root);

    public static IFileProvider CreateFileProvider(string? webRootPath) =>
        CreateFileProvider().WithWebRoot(webRootPath);

    public static IFileProvider CreateFileProvider(string? webRootPath, ModuleCatalog modules) =>
        new CompositeFileProvider(
            [
                CreateFileProvider(),
                ..modules.Frontends.Select(frontend => new ManifestEmbeddedFileProvider(frontend.Assembly, Root))
            ]).WithWebRoot(webRootPath);
}
