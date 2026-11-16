using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<Gallery> Galleries => Set<Gallery>();

    public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();
}
