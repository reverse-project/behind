using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed record DownloadUploadLimitMetadata;

public static class DownloadUploadLimitExtensions
{
    private const long RequestOverheadBytes = 1024 * 1024;

    extension(IApplicationBuilder application)
    {
        public IApplicationBuilder UseDownloadUploadLimit() =>
            application.Use((context, next) =>
            {
                if (context.GetEndpoint()?.Metadata.GetMetadata<DownloadUploadLimitMetadata>() is not null)
                {
                    context.ApplyDownloadUploadLimit(
                        context.RequestServices.GetRequiredService<IOptionsMonitor<DownloadsConfiguration>>().CurrentValue.FileLimit);
                }

                return next(context);
            });
    }

    extension(HttpContext context)
    {
        private void ApplyDownloadUploadLimit(long fileLimit)
        {
            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
            {
                bodySize.MaxRequestBodySize = fileLimit * DownloadEndpoints.MaxUploadFiles + RequestOverheadBytes;
            }

            context.Features.Set<IFormFeature>(
                new FormFeature(
                    context.Request,
                    new FormOptions { MultipartBodyLengthLimit = fileLimit + RequestOverheadBytes }));
        }
    }
}
