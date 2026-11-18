using Behind.Common.Database;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Data.Common;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class AuthenticationConflictPostgresTests
{
    [Theory]
    [InlineData(409, true, false)]
    [InlineData(204, true, false)]
    [InlineData(409, false, false)]
    [InlineData(204, false, false)]
    [InlineData(409, false, true)]
    [InlineData(204, false, true)]
    public async Task SerializationFailureDiscardsWritesAndCommitActions(int status, bool duringCommit, bool rollbackFails)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]!).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var cancellationToken = timeout.Token;
        await postgres.StartAsync(cancellationToken);
        var observer = new RollbackObserver(rollbackFails);
        var options = new DbContextOptionsBuilder<ConflictDatabase>()
            .UseNpgsql(postgres.GetConnectionString()).AddInterceptors(observer).Options;
        await using var database = new ConflictDatabase(options);
        await database.Database.ExecuteSqlRawAsync("CREATE TABLE attempts (id integer PRIMARY KEY)", cancellationToken);
        if (duringCommit)
        {
            await database.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fail_commit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'serialization conflict' USING ERRCODE = '40001'; END $$;
                CREATE CONSTRAINT TRIGGER fail_commit AFTER INSERT ON attempts
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fail_commit();
                """, cancellationToken);
        }
        var context = new DefaultHttpContext { RequestAborted = cancellationToken };
        context.SetEndpoint(new Endpoint(null,
            new EndpointMetadataCollection(new AuthenticationTransactionMetadata(status)), null));
        context.Response.Body = new MemoryStream();
        var published = 0;
        var released = 0;
        var middleware = new AuthenticationTransactionMiddleware(async http =>
        {
            var transaction = http.Features.Get<AuthenticationTransaction>()!;
            await transaction.BeginAsync(cancellationToken);
            transaction.OnCommitted(() => published++);
            transaction.OnRolledBack(() => released++);
            await database.Database.ExecuteSqlRawAsync("INSERT INTO attempts VALUES (1)", cancellationToken);
            http.Response.Headers["X-Uncommitted"] = "discard";
            await http.Response.WriteAsync("uncommitted", cancellationToken);
            if (!duringCommit)
            {
                await database.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN RAISE EXCEPTION 'serialization conflict' USING ERRCODE = '40001'; END $$;
                    """, cancellationToken);
            }
        });

        await middleware.InvokeAsync(context, database);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.False(context.Response.Headers.ContainsKey("X-Uncommitted"));
        Assert.Equal(0, published);
        Assert.Equal(1, released);
        Assert.Equal(duringCommit ? 0 : 1, observer.Attempts);
        await using var verify = new ConflictDatabase(options);
        Assert.Equal(0, await verify.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM attempts")
            .SingleAsync(cancellationToken));
    }

    private sealed class RollbackObserver(bool fail) : DbTransactionInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (fail)
            {
                throw new InvalidOperationException("Rollback failed");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ConflictDatabase(DbContextOptions<ConflictDatabase> options) : DbContext(options), IUnitOfWork
    {
        public IRepository<TAggregate, TKey> GetRepository<TAggregate, TKey>() => throw new NotSupportedException();
        public IHardDeleteRepository<TAggregate, TKey> GetHardDeleteRepository<TAggregate, TKey>() => throw new NotSupportedException();
        public IRestoreRepository<TAggregate, TKey> GetRestoreRepository<TAggregate, TKey>() => throw new NotSupportedException();
    }
}
