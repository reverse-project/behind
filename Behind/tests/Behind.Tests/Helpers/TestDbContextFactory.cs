using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

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

        using var versioning = CreateVersioningContext();
        versioning.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    public CoreContextSqlite CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoreContextSqlite>()
            .UseSqlite(_connection)
            .Options;

        return new CoreContextSqlite(options);
    }

    public VersioningContextSqlite CreateVersioningContext() =>
        new(new DbContextOptionsBuilder<VersioningContextSqlite>().UseSqlite(_connection).Options);

    public VersioningContextSqlite CreateAuditedVersioningContext(AuditActor actor, TimeProvider? timeProvider = null)
    {
        var interceptor = new AuditSaveChangesInterceptor(
            () => new FixedAuditActorProvider(actor),
            timeProvider ?? TimeProvider.System
        );

        var options = new DbContextOptionsBuilder<VersioningContextSqlite>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        return new VersioningContextSqlite(options);
    }

    public CoreContextSqlite CreateAuditedContext(AuditActor actor, TimeProvider? timeProvider = null)
    {
        var interceptor = new AuditSaveChangesInterceptor(
            () => new FixedAuditActorProvider(actor),
            timeProvider ?? TimeProvider.System
        );

        var options = new DbContextOptionsBuilder<CoreContextSqlite>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        return new CoreContextSqlite(options);
    }

    public void Dispose() => _connection.Dispose();
}
