using System.Net;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public class DownloadUploadLimitTests
{
    private const int Megabyte = 1024 * 1024;

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(ReloadableMemoryConfiguration configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.Configure<DownloadsConfiguration>(configuration.GetSection(DownloadsConfiguration.SectionName));
        var app = builder.Build();
        app.UseDownloadUploadLimit();
        app.MapPost("/upload", async (HttpRequest request) =>
            {
                var form = await request.ReadFormAsync();

                return Results.Ok(form.Files.Count);
            })
            .DisableAntiforgery()
            .WithMetadata(new DownloadUploadLimitMetadata());
        app.MapPost("/other", async (HttpRequest request) => Results.Ok((await request.ReadFormAsync()).Files.Count))
            .DisableAntiforgery();
        await app.StartAsync();

        return (app, new HttpClient { BaseAddress = new Uri(app.Urls.First()) });
    }

    private static MultipartFormDataContent FileOf(int bytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[bytes]), "files", "file.bin");

        return content;
    }

    private static ReloadableMemoryConfiguration Limit(long bytes) =>
        new(new Dictionary<string, string?> { [$"{DownloadsConfiguration.SectionName}:MaxFileBytes"] = bytes.ToString() });

    [Fact]
    public async Task UseDownloadUploadLimit_AcceptsAFileWithinTheConfiguredLimit()
    {
        var (app, client) = await StartAsync(Limit(2 * Megabyte));
        await using var _ = app;
        using var __ = client;

        var response = await client.PostAsync("/upload", FileOf(Megabyte));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UseDownloadUploadLimit_RefusesAFileOverTheConfiguredLimit()
    {
        var (app, client) = await StartAsync(Limit(1));
        await using var _ = app;
        using var __ = client;

        var response = await client.PostAsync("/upload", FileOf(2 * Megabyte));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UseDownloadUploadLimit_FollowsAChangedLimitWithoutARestart()
    {
        var configuration = Limit(1);
        var (app, client) = await StartAsync(configuration);
        await using var _ = app;
        using var __ = client;

        var before = (await client.PostAsync("/upload", FileOf(2 * Megabyte))).StatusCode;
        configuration.Update($"{DownloadsConfiguration.SectionName}:MaxFileBytes", (3 * Megabyte).ToString());
        var after = (await client.PostAsync("/upload", FileOf(2 * Megabyte))).StatusCode;

        Assert.NotEqual(HttpStatusCode.OK, before);
        Assert.Equal(HttpStatusCode.OK, after);
    }

    [Fact]
    public async Task UseDownloadUploadLimit_LeavesOtherEndpointsAlone()
    {
        var (app, client) = await StartAsync(Limit(1));
        await using var _ = app;
        using var __ = client;

        var response = await client.PostAsync("/other", FileOf(Megabyte / 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
