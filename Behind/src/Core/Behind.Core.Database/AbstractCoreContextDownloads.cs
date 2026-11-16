using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<DownloadFolder> DownloadFolders => Set<DownloadFolder>();

    public DbSet<Download> Downloads => Set<Download>();
}
