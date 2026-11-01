using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<Site> Sites => Set<Site>();

    public DbSet<SiteImage> SiteImages => Set<SiteImage>();

    public Guid GetKey(SiteImage image) => Entry(image).Property<Guid>("Id").CurrentValue;
}
