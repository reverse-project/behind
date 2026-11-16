using Behind.Common.Modules;
using Behind.Core.Database;
using Behind.Sport.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sites.FSE.Database.Sqlite;

public sealed class FseContextSqlite(DbContextOptions<FseContextSqlite> options, ModuleCatalog modules)
    : AbstractCoreContext(options, modules), ISportContext
{
    public DbSet<Discipline> Disciplines => Set<Discipline>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<StatType> StatTypes => Set<StatType>();

    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<PlayerSeason> PlayerSeasons => Set<PlayerSeason>();

    public DbSet<Stat> Stats => Set<Stat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyCoreProviderConventions();
    }
}
