using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<Message> Messages => Set<Message>();
}
