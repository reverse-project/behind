using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public class DownloadRoutesTests
{
    private static List<RouteEndpoint> Routes(long? maxBytes = null)
    {
        var builder = WebApplication.CreateBuilder();

        if (maxBytes is { } limit)
        {
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [$"{DownloadsConfiguration.SectionName}:MaxFileBytes"] = limit.ToString() });
        }

        builder.Services.Configure<DownloadsConfiguration>(
            builder.Configuration.GetSection(DownloadsConfiguration.SectionName));

        builder.Services.AddScoped(_ => Substitute.For<IDownloadQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IDownloadFolderCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IDownloadCommands>());
        builder.Services.AddScoped(_ => Substitute.For<ISessionQueries>());

        var app = builder.Build();
        app.MapDownloadEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static RouteEndpoint Find(List<RouteEndpoint> routes, string method, string pattern) =>
        routes.Single(route =>
            route.RoutePattern.RawText == pattern
            && route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

    private static string? Policy(RouteEndpoint route) =>
        route.Metadata.GetOrderedMetadata<IAuthorizeData>().SingleOrDefault()?.Policy;

    [Theory]
    [InlineData("GET", "/downloads/folders")]
    [InlineData("GET", "/downloads/folders/{id:guid}/files")]
    [InlineData("GET", "/downloads/files/{id:guid}")]
    public void Browsing_IsPublic(string method, string pattern)
    {
        Assert.Empty(Find(Routes(), method, pattern).Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    [Theory]
    [InlineData("POST", "/downloads/folders", "Download.Write")]
    [InlineData("POST", "/downloads/folders/{id:guid}/folders", "Download.Write")]
    [InlineData("POST", "/downloads/folders/{id:guid}", "Download.Write")]
    [InlineData("POST", "/downloads/folders/{id:guid}/files", "Download.Write")]
    [InlineData("POST", "/downloads/files/{id:guid}", "Download.Write")]
    [InlineData("POST", "/downloads/folders/{id:guid}/delete", "Download.Delete")]
    [InlineData("POST", "/downloads/files/{id:guid}/delete", "Download.Delete")]
    [InlineData("GET", "/downloads/deleted", "RawDatabase.Manage")]
    [InlineData("POST", "/downloads/folders/{id:guid}/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/downloads/folders/{id:guid}/hard/delete", "RawDatabase.Manage")]
    [InlineData("POST", "/downloads/files/{id:guid}/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/downloads/files/{id:guid}/hard/delete", "RawDatabase.Manage")]
    public void Changes_RequireTheirGrant(string method, string pattern, string policy)
    {
        Assert.Equal(policy, Policy(Find(Routes(), method, pattern)));
    }

    [Fact]
    public void Upload_AcceptsRequestsOfTheConfiguredFileSizeForEveryFileOfABatch()
    {
        var route = Find(Routes(5_000_000), "POST", "/downloads/folders/{id:guid}/files");

        var request = route.Metadata.GetMetadata<IRequestSizeLimitMetadata>();
        var form = route.Metadata.GetMetadata<RequestFormLimitsAttribute>();

        Assert.Equal(5_000_000L * DownloadEndpoints.MaxUploadFiles + 1024 * 1024, request!.MaxRequestBodySize);
        Assert.Equal(5_000_000L + 1024 * 1024, form!.MultipartBodyLengthLimit);
    }

    [Fact]
    public void Upload_WithoutAConfiguredLimit_UsesTheDefaultFileSize()
    {
        var route = Find(Routes(), "POST", "/downloads/folders/{id:guid}/files");

        Assert.Equal(
            DownloadsConfiguration.DefaultMaxFileBytes + 1024 * 1024,
            route.Metadata.GetMetadata<RequestFormLimitsAttribute>()!.MultipartBodyLengthLimit);
    }
}
