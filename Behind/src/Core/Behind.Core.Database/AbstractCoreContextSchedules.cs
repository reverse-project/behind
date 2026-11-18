using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<ScheduleRun> ScheduleRuns => Set<ScheduleRun>();
}
