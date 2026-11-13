using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<UserReport> UserReports => Set<UserReport>();

    public DbSet<ReportEmailDelivery> ReportEmailDeliveries => Set<ReportEmailDelivery>();
}
