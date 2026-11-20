using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.Middleware;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreWebApi.Identity;

[Trait("Category", "Integration")]
public sealed class AuthTransactionsPostgresIntegrationTests
{
    [Fact]
    public async Task RegistrationIsInvisibleUntilApprovalAndRolesAreCommitted()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.")).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var context = new CoreContextPostgres(options);
        await context.Database.EnsureCreatedAsync();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var roles = Substitute.For<IRoleCommands>();
        roles.AssignBuiltIn(Arg.Any<Guid>(), Arg.Any<BuiltInRole>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                saved.SetResult();
                await resume.Task;
                await new RoleCommands(context, AccessCatalog.Core).AssignBuiltIn(
                    call.Arg<Guid>(), call.Arg<BuiltInRole>(), call.Arg<CancellationToken>());
            });
        var store = new IdentityStore(context, new UserQueries(context), new EmailQueries(context),
            new EmailCommands(context, TimeProvider.System), new PasswordQueries(context), new RoleQueries(context),
            new PasswordCommands(context), new UserCommands(context), new UserApprovalQueries(context),
            new UserApprovalCommands(context), roles, NullLogger<IdentityStore>.Instance);
        var user = new ApplicationUser { DisplayName = "Pending", Emails = [new Email("pending@test.example")], PendingApproval = true };
        var registration = store.CreateAsync(user, CancellationToken.None);
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await using var observer = new CoreContextPostgres(options);
        Assert.Equal(None, await new UserQueries(observer).GetUserByEmail("pending@test.example"));
        resume.SetResult();
        Assert.True((await registration).Succeeded);
        Assert.True(await new UserApprovalQueries(observer).IsPending(user.Id));
        Assert.Contains(await new RoleQueries(observer).GetRolesByUserId(user.Id),
            role => role.Name == BuiltInAccess.Users.Role.Name);
    }
    [Fact]
    public async Task ConcurrentRemovalsCannotRemoveEveryRequiredFactor()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.")).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var seed = new CoreContextPostgres(options);
        await seed.Database.EnsureCreatedAsync();
        var user = new HumanUser("Factors", [new Email("factors@test.example")], None);
        seed.Users.Add(user);
        await seed.SaveChangesAsync();
        var userId = seed.Entry(user).Property<Guid>("Id").CurrentValue;
        var commands = new TwoFactorCommands(seed);
        await commands.AddPasskey(userId, new UserPasskey("first", "first", "key", 0));
        await commands.AddPasskey(userId, new UserPasskey("second", "second", "key", 0));
        var keys = await new TwoFactorQueries(seed).GetPasskeys(userId);
        var arrived = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Remove(Guid key)
        {
            await using var context = new CoreContextPostgres(options);
            var actual = new TwoFactorQueries(context);
            var queries = Substitute.For<ITwoFactorQueries>();
            queries.GetStatus(userId, Arg.Any<CancellationToken>()).Returns(async _ =>
            {
                var status = await actual.GetStatus(userId);
                if (Interlocked.Increment(ref arrived) == 2)
                {
                    ready.SetResult();
                }
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
                return status;
            });
            var enrollment = new TwoFactorEnrollment(queries, new TwoFactorCommands(context),
                null!, null!, null!, Options.Create(new SecurityConfiguration { TwoFactorRequired = true }), TimeProvider.System);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(new Endpoint(null,
                new EndpointMetadataCollection(new AuthenticationTransactionMetadata(StatusCodes.Status409Conflict)), null));
            var middleware = new AuthenticationTransactionMiddleware(async request =>
            {
                await new AuthenticationTransactionFilter().InvokeAsync(
                    new DefaultEndpointFilterInvocationContext(request, []),
                    async _ =>
                    {
                        Assert.Equal(TwoFactorRemoval.Removed, await enrollment.RemovePasskey(userId, key));
                        return Results.NoContent();
                    });
                request.Response.StatusCode = StatusCodes.Status204NoContent;
            });
            await middleware.InvokeAsync(httpContext, context);
            Assert.Contains(httpContext.Response.StatusCode,
                new[] { StatusCodes.Status204NoContent, StatusCodes.Status409Conflict });
            return httpContext.Response.StatusCode == StatusCodes.Status204NoContent;
        }
        var removed = await Task.WhenAll(Remove(keys[0].Id), Remove(keys[1].Id));
        Assert.Single(removed, value => value);
        Assert.Single(await new TwoFactorQueries(seed).GetPasskeys(userId));
    }

}
