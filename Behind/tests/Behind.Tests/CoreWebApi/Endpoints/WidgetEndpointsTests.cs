using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class WidgetEndpointsTests
{
    [Fact]
    public async Task Downloads_SortsGloballyByUploadTimeRatherThanFolderOrder()
    {
        var firstFolder = Guid.NewGuid();
        var secondFolder = Guid.NewGuid();
        var newest = Guid.NewGuid();
        var oldest = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var queries = Substitute.For<IDownloadQueries>();
        queries.ListFolders(default).ReturnsForAnyArgs(new IDownloadQueries.DownloadFolderInfo[]
        {
            new(firstFolder, None, "first", "First"),
            new(secondFolder, None, "second", "Second")
        });
        queries.ListFiles(firstFolder, default).Returns(
            Some<IReadOnlyList<IDownloadQueries.DownloadInfo>>([
                new(oldest, firstFolder, "Old", "old.txt", "text/plain", 1, "Ada", Some(now.AddDays(-1)))
            ]));
        queries.ListFiles(secondFolder, default).Returns(
            Some<IReadOnlyList<IDownloadQueries.DownloadInfo>>([
                new(newest, secondFolder, "New", "new.txt", "text/plain", 1, "Ada", Some(now))
            ]));

        var result = Assert.IsType<Ok<DownloadFilesResponse>>(await WidgetEndpoints.LatestDownloads(queries, default));

        Assert.Equal([newest, oldest], result.Value!.Files.Select(file => file.Id));
    }

    [Fact]
    public void Sanitize_RemovesScriptAndUnsafeAttributes()
    {
        var result = Assert.IsType<Ok<WidgetEndpoints.HtmlResponse>>(
            WidgetEndpoints.Sanitize(new("<p onclick=\"alert(1)\">Hello<script>alert(1)</script></p>")));

        Assert.Contains("Hello", result.Value!.Content);
        Assert.DoesNotContain("onclick", result.Value.Content);
        Assert.DoesNotContain("<script", result.Value.Content);
    }

}
