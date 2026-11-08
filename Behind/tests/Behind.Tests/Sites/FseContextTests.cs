using Behind.Common.Modules;
using Behind.Core.Database;
using Behind.Sites.FSE.Database.Postgres;
using Behind.Sites.FSE.Database.Sqlite;
using Behind.Sport.Database;
using Behind.Sport.Domain;
using Behind.Sport.WebApi;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.Sites;

public class FseContextTests
{
    private static readonly Type[] SportEntities =
    [
        typeof(Discipline), typeof(Position), typeof(StatType), typeof(Season),
        typeof(Team), typeof(Player), typeof(PlayerSeason), typeof(Stat)
    ];

    private static ModuleCatalog Catalog() =>
        ModuleCatalog.Create([SportModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    private static DbContext Sqlite() =>
        new FseContextSqlite(
            new DbContextOptionsBuilder<FseContextSqlite>().UseSqlite("Data Source=:memory:").Options,
            Catalog());

    private static DbContext Postgres() =>
        new FseContextPostgres(
            new DbContextOptionsBuilder<FseContextPostgres>().UseNpgsql("Host=localhost;Database=fse").Options,
            Catalog());

    [Fact]
    public void Contexts_ImplementTheCoreAndSportContexts()
    {
        using var sqlite = Sqlite();
        using var postgres = Postgres();

        Assert.IsAssignableFrom<AbstractCoreContext>(sqlite);
        Assert.IsAssignableFrom<ISportContext>(sqlite);
        Assert.IsAssignableFrom<AbstractCoreContext>(postgres);
        Assert.IsAssignableFrom<ISportContext>(postgres);
    }

    [Fact]
    public void SqliteModel_ContainsTheSportEntities()
    {
        using var context = Sqlite();

        Assert.All(SportEntities, entity => Assert.NotNull(context.Model.FindEntityType(entity)));
    }

    [Fact]
    public void PostgresModel_ContainsTheSportEntities()
    {
        using var context = Postgres();

        Assert.All(SportEntities, entity => Assert.NotNull(context.Model.FindEntityType(entity)));
    }
}
