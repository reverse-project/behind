using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Behind.Common.WebApi.Infrastructure.StaticFiles;

public static class StaticFrontendExtensions
{
    private const string NoSniff = "nosniff";
    private const string ReferrerPolicyHeader = "Referrer-Policy";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddStaticFrontend(FrontendOptions? options = null)
        {
            var effective = options ?? FrontendOptions.Default;

            services.AddSingleton(Options.Create(effective));

            return effective.Compress
                ? services.AddResponseCompression(compression =>
                {
                    compression.EnableForHttps = true;
                    compression.MimeTypes = effective.CompressedMimeTypes;
                })
                : services;
        }
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseStaticFrontend(IFileProvider fileProvider)
        {
            var options = app.ApplicationServices.GetRequiredService<IOptions<FrontendOptions>>().Value;
            var provider = options.Minify
                ? new MinifyingFileProvider(fileProvider, ContentMinifiers.Default)
                : fileProvider;

            return (options.Compress ? app.UseResponseCompression() : app)
                .UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = provider,
                    DefaultFileNames = [options.DefaultFile]
                })
                .UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = provider,
                    OnPrepareResponse = context =>
                    {
                        context.Context.Response.Headers.CacheControl = options.CacheControl;
                        context.Context.Response.Headers.XContentTypeOptions = NoSniff;
                        context.Context.Response.Headers.Append(ReferrerPolicyHeader, options.ReferrerPolicy);
                        context.Context.Response.Headers.ContentSecurityPolicy = options.ContentSecurityPolicy;
                    }
                });
        }
    }
}
