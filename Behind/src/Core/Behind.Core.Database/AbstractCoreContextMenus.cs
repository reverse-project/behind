using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<Menu> Menus => Set<Menu>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<Dictionary<string, object>> MenuItemRoles => Set<Dictionary<string, object>>(MenuItemConfiguration.RolesEntity);
}
