using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<News> NewsEntries => Set<News>();

    public DbSet<NewsCategory> NewsCategories => Set<NewsCategory>();

    public DbSet<NewsComment> NewsComments => Set<NewsComment>();

    public DbSet<ActiveVersion> ActiveVersions => Set<ActiveVersion>();
}
