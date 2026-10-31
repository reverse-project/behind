using Microsoft.Extensions.FileProviders;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public static class FileProviderExtensions
{
    extension(IFileProvider fileProvider)
    {
        public IFileProvider WithWebRoot(string? webRootPath) =>
            !string.IsNullOrWhiteSpace(webRootPath) && Directory.Exists(webRootPath)
                ? new CompositeFileProvider(new PhysicalFileProvider(webRootPath), fileProvider)
                : fileProvider;
    }
}
