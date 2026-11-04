using System.Net;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.MagicLinks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.CoreWebApi.MagicLinks;

public sealed class MagicLinkProtectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private static MagicLinkSettings Settings(int limit) =>
        new(Uuid.CreateVersion7(Now), None, RequestLimit: Some(limit), RequestWindow: Some(TimeSpan.FromMinutes(10)));

    [Fact]
    public async Task RateLimit_RejectsRequestsOverTheLimitWith429()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IMagicLinkSettingsStore>(new MagicLinkSettingsStore(Some(Settings(2)))).AddMagicLinkRateLimiting();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapPost("/limited", () => Results.NoContent()).RequireRateLimiting(MagicLinkExtensions.RequestRateLimitPolicy);
        app.MapPost("/free", () => Results.NoContent());
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var statuses = new List<HttpStatusCode>();
        foreach (var _ in Enumerable.Range(0, 3))
        {
            statuses.Add((await client.PostAsync("/limited", null)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/free", null)).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task RedeemRateLimit_RejectsAttemptsOverTheLimitWith429AndIsIndependentOfRequestLimit()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IMagicLinkSettingsStore>(new MagicLinkSettingsStore(Some(Settings(1) with { RedeemLimit = Some(2) }))).AddMagicLinkRateLimiting();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapPost("/redeem", () => Results.NoContent()).RequireRateLimiting(MagicLinkExtensions.RedeemRateLimitPolicy);
        app.MapPost("/request", () => Results.NoContent()).RequireRateLimiting(MagicLinkExtensions.RequestRateLimitPolicy);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var statuses = new List<HttpStatusCode>();
        foreach (var _ in Enumerable.Range(0, 3))
        {
            statuses.Add((await client.PostAsync("/redeem", null)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/request", null)).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task Cleanup_DeletesExpiredLinksUsingTheClock()
    {
        var commands = Substitute.For<IMagicLinkCommands>();
        commands.DeleteExpired(Now, Arg.Any<CancellationToken>()).Returns(3);
        var services = new ServiceCollection().AddScoped(_ => commands).BuildServiceProvider();
        using var service = new MagicLinkCleanupService(
            services.GetRequiredService<IServiceScopeFactory>(),
            new Helpers.FixedTimeProvider(Now),
            Substitute.For<ILogger<MagicLinkCleanupService>>());

        Assert.Equal(3, await service.CleanAsync());
    }

    [Fact]
    public async Task Cleanup_RunsOnStartupAndSurvivesFailures()
    {
        var commands = Substitute.For<IMagicLinkCommands>();
        var called = new TaskCompletionSource();
        commands.DeleteExpired(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                called.TrySetResult();
                return Task.FromException<int>(new InvalidOperationException("db"));
            });
        var logger = Substitute.For<ILogger<MagicLinkCleanupService>>();
        var services = new ServiceCollection().AddScoped(_ => commands).BuildServiceProvider();
        using var service = new MagicLinkCleanupService(
            services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            logger);

        await service.StartAsync(CancellationToken.None);
        await called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void AddMagicLink_RegistersCleanupAndMailServices()
    {
        var services = new ServiceCollection();

        services.AddMagicLink(new FileInfo("missing.json"));

        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService)
                                       && d.ImplementationType == typeof(MagicLinkCleanupService));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService)
                                       && d.ImplementationType == typeof(MagicLinkMailService));
        Assert.Contains(services, d => d.ServiceType == typeof(IMagicLinkMailQueue));
    }

    [Fact]
    public async Task RateLimit_WithoutMagicLinkSettings_UsesTheDefaultLimits()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IMagicLinkSettingsStore>(new MagicLinkSettingsStore(None)).AddMagicLinkRateLimiting();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapPost("/limited", () => Results.NoContent()).RequireRateLimiting(MagicLinkExtensions.RequestRateLimitPolicy);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var statuses = new List<HttpStatusCode>();
        foreach (var _ in Enumerable.Range(0, MagicLinkSettings.DefaultRequestLimit + 1))
        {
            statuses.Add((await client.PostAsync("/limited", null)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses.Last());
        Assert.All(statuses.Take(MagicLinkSettings.DefaultRequestLimit), status => Assert.Equal(HttpStatusCode.NoContent, status));
        await app.StopAsync();
    }

    [Fact]
    public async Task RateLimit_AfterTheSettingsChange_AppliesTheNewLimitWithoutARestart()
    {
        var store = new MagicLinkSettingsStore(Some(Settings(1)));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IMagicLinkSettingsStore>(store).AddMagicLinkRateLimiting();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapPost("/limited", () => Results.NoContent()).RequireRateLimiting(MagicLinkExtensions.RequestRateLimitPolicy);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var before = (await client.PostAsync("/limited", null)).StatusCode;
        var blocked = (await client.PostAsync("/limited", null)).StatusCode;
        store.Set(Some(Settings(3)));
        var after = (await client.PostAsync("/limited", null)).StatusCode;

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests, HttpStatusCode.NoContent], [before, blocked, after]);
        await app.StopAsync();
    }
}
