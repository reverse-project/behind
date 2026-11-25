using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Behind.Common.WebApi.Filters;
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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountPinHttpTests
{
    [Fact]
    public async Task HttpValidationHonorsConfiguredPinLimitAbove128()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(Options.Create(new SecurityConfiguration
        {
            Accounts = new SecurityAccountSettings { SwitchPinMaxLength = 200 }
        }));
        await using var app = builder.Build();
        var group = app.MapGroup("/pins").AddEndpointFilter(new SubaccountFeatureFilter(false));
        group.MapPost("/create", (SubaccountEndpoints.CreateRequest _) => Results.NoContent())
            .AddEndpointFilter<DataAnnotationValidationFilter<SubaccountEndpoints.CreateRequest>>();
        group.MapPost("/update", (SubaccountEndpoints.UpdateRequest _) => Results.NoContent())
            .AddEndpointFilter<DataAnnotationValidationFilter<SubaccountEndpoints.UpdateRequest>>();
        group.MapPost("/pin", (SubaccountEndpoints.PinRequest _) => Results.NoContent())
            .AddEndpointFilter<DataAnnotationValidationFilter<SubaccountEndpoints.PinRequest>>();
        group.MapPost("/switch", (SubaccountEndpoints.SwitchRequest _) => Results.NoContent())
            .AddEndpointFilter<DataAnnotationValidationFilter<SubaccountEndpoints.SwitchRequest>>();
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(url) };

        foreach (var (pin, expected) in new[]
        {
            (new string('1', 200), HttpStatusCode.NoContent),
            (new string('1', 201), HttpStatusCode.BadRequest)
        })
        {
            var requests = new (string Path, object Body)[]
            {
                ("/pins/create", new SubaccountEndpoints.CreateRequest("Child", null, null, true, pin)),
                ("/pins/create", new SubaccountEndpoints.CreateRequest("Child", null, null, true, null, pin)),
                ("/pins/update", new SubaccountEndpoints.UpdateRequest("Child", null, true, pin, false)),
                ("/pins/pin", new SubaccountEndpoints.PinRequest(pin, false)),
                ("/pins/switch", new SubaccountEndpoints.SwitchRequest(pin))
            };
            foreach (var (path, body) in requests)
            {
                using var response = await client.PostAsJsonAsync(path, body);
                Assert.Equal(expected, response.StatusCode);
            }
        }
    }

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
        await VerifyPostgresChanges(false);
    }

    [Fact]
    public async Task SqliteHttpRequestsValidateFirstSetupAndProtectStoredPin()
    {
        using var factory = new TestDbContextFactory();
        await VerifyChanges(factory.CreateContext, false, linked: true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentFirstSetupsCanOnlyCommitOnePin()
    {
        await VerifyPostgresChanges(true);
    }

    private static async Task VerifyPostgresChanges(bool linked)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.")).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString());
        if (linked)
        {
            options.AddInterceptors(new ConcurrentPinInsertGate());
        }
        await using var seed = new CoreContextPostgres(options.Options);
        await seed.Database.MigrateAsync();
        await VerifyChanges(() => new CoreContextPostgres(options.Options), true, linked);
    }

    private static async Task VerifyChanges(Func<AbstractCoreContext> contextFactory, bool concurrent, bool linked = false)
    {
        await using var seed = contextFactory();
        var primary = new HumanUser("Primary", [new Email("primary@example.test")], None);
        var actor = primary;
        seed.Users.Add(primary);
        if (linked)
        {
            actor = new HumanUser("Linked", [new Email("linked@example.test")], None);
            seed.Users.Add(actor);
            seed.Subaccounts.Add(new Subaccount(primary, actor, false));
        }
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
        builder.Services.AddSingleton(Options.Create(new SecurityConfiguration
        {
            Accounts = new SecurityAccountSettings { SwitchPinMaxLength = 4 }
        }));
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
        app.MapPut("/profile/subaccounts/{userId:guid}/pin", SubaccountEndpoints.ChangePin)
            .WithMetadata(new SubaccountTransactionMetadata())
            .AddEndpointFilter(new SubaccountFeatureFilter(false))
            .AddEndpointFilter<DataAnnotationValidationFilter<SubaccountEndpoints.PinRequest>>()
            .RequireAuthenticationTransaction();
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(url) };
        async Task<HttpStatusCode> Change(string pin, SubaccountEndpoints.PinProof? proof)
        {
            using var result = await client.PutAsJsonAsync($"/profile/subaccounts/{actorId}/pin",
                new SubaccountEndpoints.PinRequest(pin, false, proof));
            return result.StatusCode;
        }

        SubaccountEndpoints.PinProof? proof = null;
        if (linked)
        {
            Assert.Equal(HttpStatusCode.BadRequest, await Change("12345", null));
            Assert.Equal(HttpStatusCode.BadRequest, await Change("abcd", null));
            Assert.Null(await seed.UserSwitchPins.AsNoTracking().SingleOrDefaultAsync());
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, await Change("1234", null));
            Assert.Equal(HttpStatusCode.Forbidden, await Change("1234", new(Password: "wrong-password")));
            Assert.Equal(HttpStatusCode.NoContent, await Change("1234", new(Password: "actor-password")));
            proof = new(CurrentPin: "1234");
        }
        var results = concurrent
            ? await Task.WhenAll(Change("5678", proof), Change("9876", proof))
            : new[] { await Change("5678", proof), await Change("9876", proof) };
        Assert.Single(results, status => status == HttpStatusCode.NoContent);
        Assert.Single(results, status => status is HttpStatusCode.Forbidden or HttpStatusCode.Conflict);
        if (linked && concurrent)
        {
            Assert.Contains(HttpStatusCode.Conflict, results);
        }
        var stored = await seed.UserSwitchPins.AsNoTracking().SingleAsync();
        Assert.True(PasswordHash.CreateFromExisting(stored.PinHash.ToString()).Verify(
            results[0] == HttpStatusCode.NoContent ? "5678" : "9876"));
    }

    private sealed class ConcurrentPinInsertGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource bothReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int pendingInserts;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<UserSwitchPin>().Any(entry => entry.State == EntityState.Added) == true)
            {
                if (Interlocked.Increment(ref pendingInserts) == 2)
                {
                    bothReady.SetResult();
                }
                await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
