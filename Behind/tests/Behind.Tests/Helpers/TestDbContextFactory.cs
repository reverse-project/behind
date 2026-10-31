using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.Helpers;

/// <summary>
/// Creates a shared in-memory SQLite connection whose schema is initialised once.
/// Call <see cref="CreateContext"/> for each test to get an isolated <see cref="CoreContextSqlite"/>.
/// </summary>
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Create the schema once on the shared connection
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public CoreContextSqlite CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoreContextSqlite>()
            .UseSqlite(_connection)
            .Options;

        return new CoreContextSqlite(options);
    }

    public CoreContextSqlite CreateAuditedContext(AuditActor actor)
    {
        var interceptor = new AuditSaveChangesInterceptor(
            () => new FixedAuditActorProvider(actor),
            TimeProvider.System
        );

        var options = new DbContextOptionsBuilder<CoreContextSqlite>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        return new CoreContextSqlite(options);
    }

    public void Dispose() => _connection.Dispose();
}
