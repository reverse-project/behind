using Behind.Common.Configuration;
using Behind.Common.Modules;
using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.MarkdownViewer.WebApi;

public static class MarkdownViewerWebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddMarkdownViewer()
        {
            builder.AddReloadableJsonFile(MarkdownViewerModuleNames.ConfigurationFile, true);
            builder.Services
                .Configure<MarkdownViewerConfiguration>(
                    builder.Configuration.GetSection(MarkdownViewerConfiguration.SectionName))
                .AddSingleton<MarkdownWorkspaceProvider>()
                .AddModule(MarkdownViewerModule.Descriptor);

            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseMarkdownViewer()
        {
            application.MapMarkdownViewerEndpoints();

            return application;
        }
    }
}
