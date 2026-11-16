using System.Net;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class ConfirmationEmailRateLimitTests
{
    [Fact]
    public async Task ResendRateLimit_RejectsRequestsOverTheLimitWith429()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddConfirmationEmailRateLimiting();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapPost("/limited", () => Results.NoContent())
            .RequireRateLimiting(ConfirmationEmailExtensions.ResendRateLimitPolicy);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var statuses = new List<HttpStatusCode>();
        foreach (var _ in Enumerable.Range(0, ConfirmationEmailExtensions.ResendLimit + 1))
        {
            statuses.Add((await client.PostAsync("/limited", null)).StatusCode);
        }

        Assert.Equal(
            Enumerable.Repeat(HttpStatusCode.NoContent, ConfirmationEmailExtensions.ResendLimit)
                .Append(HttpStatusCode.TooManyRequests),
            statuses);
        await app.StopAsync();
    }
}
