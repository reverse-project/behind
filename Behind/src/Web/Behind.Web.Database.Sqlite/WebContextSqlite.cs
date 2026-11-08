using Behind.Common.Modules;
using Behind.Core.Database;
using Behind.Quotes.Database;
using Behind.Quotes.Domain;
using Behind.Sport.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Web.Database.Sqlite;

public sealed class WebContextSqlite(DbContextOptions<WebContextSqlite> options, ModuleCatalog modules)
    : AbstractCoreContext(options, modules), IQuotesContext, ISportContext
{
    public DbSet<Quote> Quotes => Set<Quote>();

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
