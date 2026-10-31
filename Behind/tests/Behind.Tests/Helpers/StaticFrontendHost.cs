using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.Helpers;

public sealed class StaticFrontendHost : IAsyncDisposable
{
    private readonly WebApplication _application;

    public HttpClient Client { get; }

    private StaticFrontendHost(WebApplication application, HttpClient client)
    {
        _application = application;
        Client = client;
    }

    public static async Task<StaticFrontendHost> StartAsync(IFileProvider fileProvider, FrontendOptions? options = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddStaticFrontend(options);

        var application = builder.Build();
        application.UseStaticFrontend(fileProvider);
        await application.StartAsync();

        var address = application.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None })
        {
            BaseAddress = new Uri(address)
        };

        return new StaticFrontendHost(application, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}
