using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountPinHttpTests
{
    [Fact]
    public async Task SqliteHttpRequestsVerifyActorPasswordAndRejectOldPin()
    {
        using var factory = new TestDbContextFactory();
        await VerifyChanges(factory.CreateContext, false);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentRequestsUsingOneOldPinCanOnlyCommitOneReplacement()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.")).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var seed = new CoreContextPostgres(options);
        await seed.Database.MigrateAsync();
        await VerifyChanges(() => new CoreContextPostgres(options), true);
    }

    private static async Task VerifyChanges(Func<AbstractCoreContext> contextFactory, bool concurrent)
    {
        await using var seed = contextFactory();
        var actor = new HumanUser("Primary", [new Email("primary@example.test")], None);
        seed.Users.Add(actor);
        var actorId = seed.GetKey(actor);
        await new PasswordCommands(seed).CreateFromUserId(actorId, new Password(PasswordHash.Create("actor-password"), true));
        await seed.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<AbstractCoreContext>(_ => contextFactory());
        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AbstractCoreContext>());
        builder.Services.AddSingleton(SessionTestHelper.SessionQueries(sessionId, actorId));
        builder.Services.AddOptions<AccountsConfiguration>();
        builder.Services.AddAuthentication("Test").AddCookie("Test", settings =>
        {
            settings.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.Use(async (http, next) =>
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", sessionId.ToString())], "Test"));
            await next(http);
        });
        app.UseAuthorization();
        app.UseMiddleware<AuthenticationTransactionMiddleware>();
        app.MapPut("/profile/subaccounts/{userId:guid}/pin", SubaccountEndpoints.ChangePin).RequireAuthenticationTransaction();
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(url) };
        async Task<HttpStatusCode> Change(string pin, SubaccountEndpoints.PinProof proof)
        {
            using var result = await client.PutAsJsonAsync($"/profile/subaccounts/{actorId}/pin",
                new SubaccountEndpoints.PinRequest(pin, false, proof));
            return result.StatusCode;
        }

        Assert.Equal(HttpStatusCode.Forbidden, await Change("1234", new(Password: "wrong-password")));
        Assert.Equal(HttpStatusCode.NoContent, await Change("1234", new(Password: "actor-password")));
        var results = concurrent
            ? await Task.WhenAll(Change("5678", new(CurrentPin: "1234")), Change("9876", new(CurrentPin: "1234")))
            : new[] { await Change("5678", new(CurrentPin: "1234")), await Change("9876", new(CurrentPin: "1234")) };
        Assert.Single(results, status => status == HttpStatusCode.NoContent);
        Assert.Single(results, status => status is HttpStatusCode.Forbidden or HttpStatusCode.Conflict);
        var stored = await seed.UserSwitchPins.AsNoTracking().SingleAsync();
        Assert.True(PasswordHash.CreateFromExisting(stored.PinHash.ToString()).Verify(
            results[0] == HttpStatusCode.NoContent ? "5678" : "9876"));
    }
}
