using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class EmailTwoFactorTransactionPostgresTests
{
    [Fact]
    public async Task FailedVerificationKeepsAttemptReservationsAndRollsBackAuthenticationWrites()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]!).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var cancellationToken = timeout.Token;
        await postgres.StartAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var seed = new CoreContextPostgres(options);
        await seed.Database.MigrateAsync(cancellationToken);
        var user = new HumanUser("Two-factor test", [new Email("two-factor@example.test")], None);
        await seed.Users.AddAsync(user, cancellationToken);
        await seed.SaveChangesAsync(cancellationToken);
        var userId = seed.Entry(user).Property<Guid>("Id").CurrentValue;
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        var tickets = new TwoFactorTickets(new EphemeralDataProtectionProvider(), time);
        var token = tickets.Seal(tickets.Create(userId, TwoFactorStage.Verify));
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var verificationCalls = 0;

        for (var attempt = 1; attempt <= EmailTwoFactor.VerificationLimit + 1; attempt++)
        {
            await using var database = new CoreContextPostgres(options);
            var context = new DefaultHttpContext
            {
                RequestAborted = cancellationToken,
                RequestServices = services
            };
            context.Request.Method = "POST";
            context.Request.Path = "/sign/in/two-factor/email/verify";
            context.Response.Body = new MemoryStream();
            context.SetEndpoint(new Endpoint(null,
                new EndpointMetadataCollection(new AuthenticationTransactionMetadata()), null));
            var middleware = new AuthenticationTransactionMiddleware(async http =>
            {
                Assert.Null(database.Database.CurrentTransaction);
                var filter = new EmailTwoFactorAttemptFilter(tickets, new MagicLinkCommands(database), time);
                var invocation = new DefaultEndpointFilterInvocationContext(http,
                    new EmailTwoFactorEndpoints.VerifyRequest(token, "invalid"));
                var result = await filter.InvokeAsync(invocation, async filtered =>
                {
                    Assert.Null(database.Database.CurrentTransaction);
                    Assert.Equal(userId, filtered.HttpContext.Features.Get<EmailTwoFactorVerificationAttempt>()?.UserId);
                    await filtered.HttpContext.Features.Get<AuthenticationTransaction>()!.BeginAsync(cancellationToken);
                    Assert.NotNull(database.Database.CurrentTransaction);
                    verificationCalls++;
                    await new PasswordCommands(database).CreateFromUserId(
                        userId, new Password($"uncommitted-{attempt}", true), cancellationToken);
                    await database.SaveChangesAsync(cancellationToken);
                    return Results.Json(new TwoFactorFailure(TwoFactorFailure.InvalidCode),
                        statusCode: StatusCodes.Status403Forbidden);
                });
                await Assert.IsAssignableFrom<IResult>(result).ExecuteAsync(http);
            });

            await middleware.InvokeAsync(context, database);

            var acceptedAttempts = Math.Min(attempt, EmailTwoFactor.VerificationLimit);
            Assert.Equal(attempt <= EmailTwoFactor.VerificationLimit
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
            Assert.Equal(acceptedAttempts, verificationCalls);
            await using var verify = new CoreContextPostgres(options);
            Assert.Equal(acceptedAttempts, await verify.MagicLinks.CountAsync(
                link => link.Function == MagicLinkFunction.EmailTwoFactor
                    && EF.Property<Guid>(link, "UserId") == userId, cancellationToken));
            Assert.Empty(await verify.Passwords.ToListAsync(cancellationToken));
        }
    }
}
