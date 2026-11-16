using Behind.Common.Database.Modules;
using Behind.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Tests.Helpers;

public sealed class Note
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;
}

public sealed class NoteLog
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;
}

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder) => builder.HasKey(note => note.Id);
}

public sealed class NoteLogConfiguration : IEntityTypeConfiguration<NoteLog>
{
    public void Configure(EntityTypeBuilder<NoteLog> builder) => builder.HasKey(log => log.Id);
}

public interface INotesContext
{
    DbSet<Note> Notes { get; }

    DbSet<NoteLog> NoteLogs { get; }
}

public sealed class NotesContextSqlite(DbContextOptions<NotesContextSqlite> options, ModuleCatalog modules)
    : AbstractCoreContext(options, modules), INotesContext
{
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<NoteLog> NoteLogs => Set<NoteLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyCoreProviderConventions();
    }
}

public sealed class NotesAuditLabels(INotesContext context) : IAuditLabelSource
{
    public async Task<IReadOnlyList<AuditLabel>> LabelsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await context.Notes
            .Where(note => ids.Contains(note.Id))
            .Select(note => new AuditLabel(note.Id, note.Title))
            .ToListAsync(cancellationToken);
}

public static class NotesModule
{
    public const string Key = "notes";
    public const string Area = "Notes";
    public static readonly Guid MenuItemId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminMenuItemId = Guid.Parse("a1000000-0000-0000-0000-000000000002");

    public static ModuleDescriptor Descriptor { get; } = new(Key, "Notes")
    {
        GrantAreas = [new ModuleGrantArea(Area, ModuleAudience.Everyone)],
        MenuItems =
        [
            new ModuleMenuItem(MenuItemId, "Notes", "#/notes", 50),
            new ModuleMenuItem(AdminMenuItemId, "Notes admin", "#/manage/notes", 60, ModuleMenuKind.User, ModuleAudience.Administration)
        ],
        AdministrationPages =
        [
            new ModuleAdministrationPage("notes.administration", "fa-note-sticky", "#/manage/notes", "Notes.Write")
        ],
        Routes =
        [
            new ModuleRoute("#/notes", "modules/notes/controllers/notes"),
            new ModuleRoute("#/manage/notes", "modules/notes/controllers/notemanager", true)
        ],
        Database = new ModuleDatabase(typeof(NotesModule).Assembly)
        {
            SoftDeletedEntities = [typeof(Note)],
            ReadOnlyEntities = [nameof(NoteLog)]
        }
    };

    public static ModuleCatalog Catalog { get; } = ModuleCatalog.Create([Descriptor]).GetOrElse(ModuleCatalog.Empty);
}

public sealed class NotesMigrationContext(DbContextOptions<NotesMigrationContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyModuleConfigurations(NotesModule.Catalog.Databases);
}

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(NotesMigrationContext))]
[Microsoft.EntityFrameworkCore.Migrations.Migration(NotesMigration.Id)]
public sealed class NotesMigration : Microsoft.EntityFrameworkCore.Migrations.Migration
{
    public const string Id = "20260101000000_CreateNotes";

    protected override void Up(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            "Note",
            table => new { Id = table.Column<Guid>(), Title = table.Column<string>() },
            constraints: table => table.PrimaryKey("PK_Note", note => note.Id));
}
