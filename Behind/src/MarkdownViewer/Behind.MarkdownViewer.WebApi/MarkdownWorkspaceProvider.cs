using Behind.Common;
using Behind.MarkdownViewer.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Behind.MarkdownViewer.WebApi;

public sealed class MarkdownWorkspaceProvider(
    IOptionsMonitor<MarkdownViewerConfiguration> options,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public Validation<MarkdownWorkspace> Viewing => options.CurrentValue.ForViewing(environment.ContentRootPath);

    public Validation<MarkdownGeneration> Generation => options.CurrentValue.ForGeneration(environment.ContentRootPath);

    public string? DefaultLocale => configuration[MarkdownViewerConfiguration.SiteDefaultLocalePath];
}
