using Behind.Common.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Postgres;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class AuthenticationPostgresIntegrationTests
{
    [Fact]
    public async Task SerializableCredentialReplacementsCannotLeaveTwoActivePasswords()
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
        var user = new HumanUser("Security test", [new Email("security@example.test")], None);
        await seed.Users.AddAsync(user, cancellationToken);
        await seed.SaveChangesAsync(cancellationToken);
        var id = seed.Entry(user).Property<Guid>("Id").CurrentValue;
        var passwords = new PasswordCommands(seed);
        await passwords.CreateFromUserId(id, new Password("original", true), cancellationToken);
        await seed.SaveChangesAsync(cancellationToken);
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;
        async Task<int> Replace(string hash)
        {
            await using var database = new CoreContextPostgres(options);
            var context = new DefaultHttpContext();
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AuthenticationTransactionMetadata()), null));
            context.RequestAborted = cancellationToken;
            context.Request.Path = "/profile/password";
            context.Request.Method = "PUT";
            context.Response.Body = new MemoryStream();
            var middleware = new AuthenticationTransactionMiddleware(async http =>
            {
                await http.Features.Get<AuthenticationTransaction>()!.BeginAsync(cancellationToken);
                var commands = new PasswordCommands(database);
                await commands.InactivateFromUserId(id, cancellationToken);
                if (Interlocked.Increment(ref readers) == 2)
                {
                    read.SetResult();
                }
                await read.Task.WaitAsync(cancellationToken);
                await commands.CreateFromUserId(id, new Password(hash, true), cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                await http.Response.WriteAsync("credential changed", cancellationToken);
            });
            try
            {
                await middleware.InvokeAsync(context, database);
                if (!read.Task.IsCompleted)
                {
                    throw new InvalidOperationException("Credential replacement completed before reaching its read gate.");
                }
            }
            catch (Exception exception)
            {
                read.TrySetException(exception);
                await timeout.CancelAsync();
                throw;
            }
            if (context.Response.StatusCode == 409)
            {
                Assert.Equal(0, context.Response.Body.Length);
            }
            return context.Response.StatusCode;
        }
        var results = await Task.WhenAll(Replace("first"), Replace("second"));
        Assert.Single(results, status => status == 200);
        Assert.Single(results, status => status == 409);
        await using var verify = new CoreContextPostgres(options);
        Assert.Equal(1, await verify.Passwords.CountAsync(password => password.IsActive
            && EF.Property<Guid>(password, "UserId") == id, cancellationToken));
        var proofRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var signingIn = new CoreContextPostgres(options);
        var signInContext = new DefaultHttpContext();
        signInContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AuthenticationTransactionMetadata()), null));
        signInContext.RequestAborted = cancellationToken;
        signInContext.Request.Path = "/sign/in/password";
        signInContext.Request.Method = "POST";
        signInContext.Response.Body = new MemoryStream();
        var pendingSignIn = new AuthenticationTransactionMiddleware(async http =>
        {
            await http.Features.Get<AuthenticationTransaction>()!.BeginAsync(cancellationToken);
            await signingIn.Passwords.Where(password => password.IsActive
                && EF.Property<Guid>(password, "UserId") == id).SingleAsync(cancellationToken);
            proofRead.SetResult();
            await recovered.Task.WaitAsync(cancellationToken);
            await new SessionCommands(signingIn).CreateForUserId(id,
                new ValidSession(AccessToken.Create(new GuestUser(None)), RefreshToken.Create(new GuestUser(None)),
                    "127.0.0.1", "stale-proof"), cancellationToken);
        }).InvokeAsync(signInContext, signingIn);
        try
        {
            await Task.WhenAny(proofRead.Task, pendingSignIn).WaitAsync(cancellationToken);
            if (pendingSignIn.IsCompleted)
            {
                await pendingSignIn;
                Assert.True(proofRead.Task.IsCompleted, "Sign-in completed before reading its proof.");
            }
            await proofRead.Task.WaitAsync(cancellationToken);
            await using (var recovery = new CoreContextPostgres(options))
            {
                var recoveryContext = new DefaultHttpContext();
                recoveryContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AuthenticationTransactionMetadata()), null));
                recoveryContext.RequestAborted = cancellationToken;
                recoveryContext.Request.Path = "/sign/in/password/reset";
                recoveryContext.Request.Method = "POST";
                recoveryContext.Response.Body = new MemoryStream();
                await new AuthenticationTransactionMiddleware(async http =>
                {
                    await http.Features.Get<AuthenticationTransaction>()!.BeginAsync(cancellationToken);
                    var replacement = new PasswordCommands(recovery);
                    await replacement.InactivateFromUserId(id, cancellationToken);
                    await replacement.CreateFromUserId(id, new Password("recovered", true), cancellationToken);
                    await recovery.SaveChangesAsync(cancellationToken);
                    await new SessionCommands(recovery).InvalidateAllSessions(id, id, DateTimeOffset.UtcNow, cancellationToken);
                }).InvokeAsync(recoveryContext, recovery);
                Assert.Equal(200, recoveryContext.Response.StatusCode);
            }
            recovered.SetResult();
            await pendingSignIn;
            Assert.Equal(409, signInContext.Response.StatusCode);
            Assert.Empty(await verify.Sessions.ToListAsync(cancellationToken));
        }
        finally
        {
            await timeout.CancelAsync();
            try
            {
                await pendingSignIn;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    [Fact]
    public async Task RestorationPreservesIndependentEmailRevocationAndSessionRevocationIncludesImpersonator()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]!).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var database = new CoreContextPostgres(options);
        await database.Database.MigrateAsync();
        var retired = new Email("revoked@example.test");
        var active = new Email("active@example.test");
        var actor = new HumanUser("Actor", [retired, active], None);
        var target = new HumanUser("Target", [new Email("target@example.test")], None);
        await database.Users.AddRangeAsync(actor, target);
        await database.SaveChangesAsync();
        var actorId = database.Entry(actor).Property<Guid>("Id").CurrentValue;
        var targetId = database.Entry(target).Property<Guid>("Id").CurrentValue;
        var retiredId = database.Entry(retired).Property<Guid>("Id").CurrentValue;
        var activeId = database.Entry(active).Property<Guid>("Id").CurrentValue;
        var now = DateTimeOffset.UtcNow;
        var receipt = new MagicLink(Guid.NewGuid(), now, DateTimeOffset.MaxValue,
            MagicLinkFunction.TwoFactorChallenge, Some(now));
        Assert.True(await new MagicLinkCommands(database).TryIssue(actorId, receipt));
        await using (var other = new CoreContextPostgres(options))
        {
            Assert.False(await new MagicLinkCommands(other).TryIssue(targetId, receipt with { }));
        }
        database.AddAuditEntry(new AuditLogEntry("Email", retiredId.ToString(), new AuditLogEntry.Deleted(), now.AddDays(-1)), AuditActor.Anonymous);
        database.AddAuditEntry(new AuditLogEntry("Email", activeId.ToString(), new AuditLogEntry.Deleted(), now), AuditActor.Anonymous);
        database.AddAuditEntry(new AuditLogEntry("User", actorId.ToString(), new AuditLogEntry.Deleted(), now), AuditActor.Anonymous);
        await database.SaveChangesAsync();
        await database.RestoreByKeyAsync<HumanUser>([actorId]);
        Assert.True(database.Restores.IsMarked(actor));
        Assert.True(database.Restores.IsMarked(active));
        Assert.False(database.Restores.IsMarked(retired));
        database.AuditLogEntries.RemoveRange(await database.AuditLogEntries.ToListAsync());
        await database.SaveChangesAsync();
        var commands = new SessionCommands(database);
        var sessionId = await commands.CreateImpersonation(actorId, targetId,
            new ValidSession(AccessToken.Create(new GuestUser(None)), RefreshToken.Create(new GuestUser(None)),
                "127.0.0.1", "security-test"));
        await commands.InvalidateAllSessions(actorId, actorId, now);
        database.ChangeTracker.Clear();
        var found = await new Behind.Core.Database.Queries.SessionQueries(database).TryFindById(sessionId);
        Assert.IsType<InvalidSession>(found.Match(() => throw new InvalidOperationException(), value => value));
    }
}
