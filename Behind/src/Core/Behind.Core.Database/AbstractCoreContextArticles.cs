using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<Article> Articles => Set<Article>();

    public DbSet<ArticleCategory> ArticleCategories => Set<ArticleCategory>();
}
