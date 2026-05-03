using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Behind.Core.Database;

public partial class AbstractCoreContext(DbContextOptions options) : DbContext(options), IUnitOfWork
{
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    
    public DbSet<HumanUser> Users => Set<HumanUser>();
    
    public DbSet<Email> Emails => Set<Email>();
    
    public DbSet<UserRole> Roles => Set<UserRole>();
    
    public DbSet<Grant>  Grants => Set<Grant>();
    
    public DbSet<WallOfShameEntry> WallOfShameEntries => Set<WallOfShameEntry>();
    public IRepository<TAggregate, TKey> GetRepository<TAggregate, TKey>() => (IRepository<TAggregate, TKey>)this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbstractCoreContext).Assembly);
        UserConfiguration.ApplyUserRoles(modelBuilder);
        RoleConfiguration.ApplyRoleGrants(modelBuilder);
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    
        optionsBuilder.ConfigureWarnings(warnings => 
            warnings.Ignore(new EventId(20609))
        );
    }
}