using Behind.Core.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.Helpers;

public sealed class NotesTestBed : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public NotesTestBed()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public NotesContextSqlite CreateContext() =>
        new(
            new DbContextOptionsBuilder<NotesContextSqlite>().UseSqlite(_connection).Options,
            NotesModule.Catalog);

    public void Dispose() => _connection.Dispose();
}
