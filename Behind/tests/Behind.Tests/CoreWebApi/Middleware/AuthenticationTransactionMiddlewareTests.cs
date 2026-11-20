using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Behind.Common.Database;
using Behind.Common.WebApi.Filters;
using Behind.Core.WebApi.Captcha;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Middleware;

public sealed class AuthenticationTransactionMiddlewareTests : IAsyncLifetime
{
    private WebApplication application = null!;
    private HttpClient client = null!;
    private readonly List<bool> transactionsDuringValidation = [];
    private int committed;
    private int rolledBack;

    public sealed record Request([property: Required] string Password, CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<IUnitOfWork>(_ => new TransactionDatabase(
            new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:").Options));
        var captcha = Substitute.For<ICaptchaService>();
        captcha.Verify(Arg.Any<CaptchaResponse?>()).Returns(call => call.Arg<CaptchaResponse?>()?.Answer == "valid");
        builder.Services.AddSingleton(captcha);
        application = builder.Build();
        application.UseMiddleware<AuthenticationTransactionMiddleware>();
        application.MapPut("/profile/password", (Request request, HttpContext context, IUnitOfWork work) =>
            {
                Assert.NotNull(((DbContext)work).Database.CurrentTransaction);
                var transaction = context.Features.Get<AuthenticationTransaction>()!;
                transaction.OnCommitted(() => committed++);
                transaction.OnRolledBack(() => rolledBack++);
                if (request.Password == "conflict")
                {
                    throw new DbUpdateConcurrencyException();
                }
                return request.Password == "rejected" ? Results.BadRequest() : Results.NoContent();
            })
            .AddEndpointFilter(async (context, next) =>
            {
                var work = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
                transactionsDuringValidation.Add(((DbContext)work).Database.CurrentTransaction is not null);
                return await next(context);
            })
            .AddEndpointFilter<DataAnnotationValidationFilter<Request>>()
            .RequireCaptcha<Request>()
            .RequireAuthenticationTransaction();
        application.MapPost("/sign/in/oidc/redeem", (HttpContext context) =>
            {
                context.Features.Get<AuthenticationTransaction>()!.OnCommitted(() =>
                {
                    Assert.False(context.Response.HasStarted);
                    context.Response.Cookies.Delete("browser-proof");
                });
                return Results.Json(new { accessToken = "committed" });
            })
            .RequireAuthenticationTransaction();
        await application.StartAsync();
        var address = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await application.DisposeAsync();
    }

    [Fact]
    public async Task JsonResponseRemainsBufferedUntilCommitCookiesAreWritten()
    {
        var response = await client.PostAsync("/sign/in/oidc/redeem", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("committed", await response.Content.ReadAsStringAsync());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("browser-proof="));
    }

    [Theory]
    [InlineData("/profile/password")]
    [InlineData("/profile/password/")]
    [InlineData("/PROFILE/PASSWORD/")]
    public async Task RoutingVariantsShareTheTransactionBoundary(string path)
    {
        var response = await client.PutAsJsonAsync(path, new Request("password", new("id", "valid")));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, committed);
        Assert.Equal(0, rolledBack);
        Assert.Equal([false], transactionsDuringValidation);
    }

    [Theory]
    [InlineData("", "valid")]
    [InlineData("password", "invalid")]
    public async Task InvalidBodyAndCaptchaAreRejectedBeforeTheTransaction(string password, string answer)
    {
        var response = await client.PutAsJsonAsync("/profile/password", new Request(password, new("id", answer)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([false], transactionsDuringValidation);
        Assert.Equal(0, committed);
        Assert.Equal(0, rolledBack);
    }

    [Theory]
    [InlineData("rejected", HttpStatusCode.BadRequest)]
    [InlineData("conflict", HttpStatusCode.Conflict)]
    public async Task FailuresReleaseReservationsWithoutRunningCommitActions(string password, HttpStatusCode status)
    {
        var response = await client.PutAsJsonAsync("/profile/password", new Request(password, new("id", "valid")));
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(0, committed);
        Assert.Equal(1, rolledBack);
    }

    private sealed class TransactionDatabase(DbContextOptions options) : DbContext(options), IUnitOfWork
    {
        public IRepository<TAggregate, TKey> GetRepository<TAggregate, TKey>() => throw new NotSupportedException();
        public IHardDeleteRepository<TAggregate, TKey> GetHardDeleteRepository<TAggregate, TKey>() => throw new NotSupportedException();
        public IRestoreRepository<TAggregate, TKey> GetRestoreRepository<TAggregate, TKey>() => throw new NotSupportedException();
    }
}
