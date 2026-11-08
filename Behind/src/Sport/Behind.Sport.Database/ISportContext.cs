using Behind.Common.Database.Modules;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database;

public interface ISportContext : IModuleContext
{
    DbSet<Discipline> Disciplines { get; }

    DbSet<Position> Positions { get; }

    DbSet<StatType> StatTypes { get; }

    DbSet<Season> Seasons { get; }

    DbSet<Team> Teams { get; }

    DbSet<Player> Players { get; }

    DbSet<PlayerSeason> PlayerSeasons { get; }

    DbSet<Stat> Stats { get; }
}
