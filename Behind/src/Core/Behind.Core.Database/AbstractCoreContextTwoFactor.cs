using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<UserTotp> UserTotps => Set<UserTotp>();

    public DbSet<UserPasskey> UserPasskeys => Set<UserPasskey>();

    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
}
