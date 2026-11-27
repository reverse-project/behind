using Microsoft.Extensions.Configuration;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public static class FrontendOptionsExtensions
{
    extension(FrontendOptions)
    {
        public static FrontendOptions Read(IConfiguration configuration)
        {
            var section = configuration.GetSection(FrontendOptions.SectionName);
            var options = section.Get<FrontendOptions>() ?? FrontendOptions.Default;
            return section.GetSection(nameof(FrontendOptions.CompressedMimeTypes)).Get<string[]>() is { } configured
                ? options with { CompressedMimeTypes = configured }
                : options;
        }
    }
}
