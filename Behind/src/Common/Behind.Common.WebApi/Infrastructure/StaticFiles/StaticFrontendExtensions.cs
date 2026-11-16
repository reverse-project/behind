using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
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
        public IServiceCollection AddStaticFrontend(FrontendOptions? options = null) =>
            services.AddSingleton(Options.Create(options ?? FrontendOptions.Default));
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseStaticFrontend(IFileProvider fileProvider) =>
            app.UseReloadable<FrontendOptions>((branch, options) =>
            {
                var provider = options.Minify
                    ? new MinifyingFileProvider(fileProvider, ContentMinifiers.Default)
                    : fileProvider;

                if (options.Compress)
                {
                    branch.UseMiddleware<ResponseCompressionMiddleware>(options.ToCompressionProvider(branch.ApplicationServices));
                }

                branch
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
            });
    }

    extension(FrontendOptions options)
    {
        private IResponseCompressionProvider ToCompressionProvider(IServiceProvider services) =>
            new ResponseCompressionProvider(
                services,
                Options.Create(new ResponseCompressionOptions
                {
                    EnableForHttps = true,
                    MimeTypes = options.CompressedMimeTypes
                }));
    }
}
