using System.Linq.Expressions;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Behind.Core.Database;

public partial class AbstractCoreContext(DbContextOptions options) : DbContext(options), IUnitOfWork, ISoftDeleteContext
{
    public SoftDeleteSwitch SoftDelete { get; } = new();

    public HardDeleteRegistry HardDeletes { get; } = new();

    public IHardDeleteRepository<TAggregate, TKey> GetHardDeleteRepository<TAggregate, TKey>() =>
        (IHardDeleteRepository<TAggregate, TKey>)this;

    public IRepository<TAggregate, TKey> GetRepository<TAggregate, TKey>() => (IRepository<TAggregate, TKey>)this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbstractCoreContext).Assembly);
        UserConfiguration.ApplyHumanUser(modelBuilder);
        UserConfiguration.ApplyUserRoles(modelBuilder);
        RoleConfiguration.ApplyRoleGrants(modelBuilder);
        modelBuilder.ApplySoftDelete(
            this,
            DeletionRecorded,
            typeof(User),
            typeof(Email),
            typeof(Role),
            typeof(Grant),
            typeof(Password),
            typeof(WallOfShameEntry));
    }

    private static readonly Expression<Func<AbstractCoreContext, string, string, bool>> DeletionRecorded =
        (context, entityName, entityKey) => context.AuditLogEntries.Any(audit =>
            audit.EntityName == entityName && audit.EntityKey == entityKey && audit.Action == new AuditLogEntry.Deleted());

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        await this.ExpandCascadingDeletesAsync(cancellationToken);
        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        HardDeletes.Clear();
        return saved;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Deletions must be saved with SaveChangesAsync.");

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(
                new EventId(20609),
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)
        );
    }
}
