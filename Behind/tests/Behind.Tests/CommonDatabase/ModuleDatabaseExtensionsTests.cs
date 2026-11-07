using Behind.Common.Database.Modules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CommonDatabase;

public sealed class ModuleDatabaseExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ModuleDatabaseExtensionsTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private NotesMigrationContext Context() =>
        new(new DbContextOptionsBuilder<NotesMigrationContext>()
            .UseSqlite(_connection, sqlite => sqlite.MigrationsHistoryTable(NotesModule.Descriptor.MigrationsHistoryTable))
            .Options);

    [Fact]
    public void MigrationsHistoryTable_IsNamedAfterTheModuleKey()
    {
        Assert.Equal("__notesMigrationsHistory", NotesModule.Descriptor.MigrationsHistoryTable);
    }

    [Fact]
    public void ApplyModuleConfigurations_AddsTheEntitiesOfTheModuleAssemblies()
    {
        using var context = Context();

        Assert.NotNull(context.Model.FindEntityType(typeof(Note)));
        Assert.NotNull(context.Model.FindEntityType(typeof(NoteLog)));
    }

    [Fact]
    public async Task ModuleMigrator_ListsAndAppliesThePendingMigrationsIntoItsOwnHistoryTable()
    {
        await using var context = Context();
        IModuleMigrator migrator = new ContextModuleMigrator<NotesMigrationContext>(NotesModule.Key, context);

        Assert.Equal(NotesModule.Key, migrator.Module);
        Assert.Equal([NotesMigration.Id], await migrator.PendingMigrationsAsync());

        await migrator.MigrateAsync();

        Assert.Empty(await migrator.PendingMigrationsAsync());
        var tables = await TablesAsync();

        Assert.Contains("Note", tables);
        Assert.Contains(NotesModule.Descriptor.MigrationsHistoryTable, tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
    }

    private async Task<IReadOnlyList<string>> TablesAsync()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();

        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public async Task AddModuleMigrator_RegistersAMigratorPerModuleContext()
    {
        var services = new ServiceCollection()
            .AddDbContext<NotesMigrationContext>(options => options.UseSqlite(_connection))
            .AddModuleMigrator<NotesMigrationContext>(NotesModule.Key);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var migrator = Assert.Single(scope.ServiceProvider.GetServices<IModuleMigrator>());

        Assert.Equal(NotesModule.Key, migrator.Module);
    }

    [Fact]
    public async Task AddModuleContext_ExposesTheProjectContextThroughTheModuleInterface()
    {
        var services = new ServiceCollection()
            .AddDbContext<Behind.Tests.Helpers.NotesContextSqlite>(options => options.UseSqlite(_connection))
            .AddSingleton(NotesModule.Catalog)
            .AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<Behind.Tests.Helpers.NotesContextSqlite>())
            .AddModuleContext<INotesContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<INotesContext>());
    }

    [Fact]
    public async Task AddModuleContext_FailsWhenTheProjectContextDoesNotImplementTheInterface()
    {
        var services = new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => Substitute.For<IUnitOfWork>())
            .AddModuleContext<INotesContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var failure = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<INotesContext>());

        Assert.Contains(nameof(INotesContext), failure.Message);
    }
}
