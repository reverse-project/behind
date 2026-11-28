using System.Linq.Expressions;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Behind.Core.Database;

public partial class AbstractCoreContext(DbContextOptions options, ModuleCatalog modules)
    : DbContext(options), IUnitOfWork, IModuleContext, IModuleModelContext, IContentRestrictionContext, IContentLocaleStore
{
    protected AbstractCoreContext(DbContextOptions options) : this(options, ModuleCatalog.Empty)
    {
    }

    public ModuleCatalog Modules { get; } = modules;

    public Guid[] HiddenContentIds { get; set; } = [];

    public DbSet<ContentRestriction> ContentRestrictions => Set<ContentRestriction>();

    public DbSet<UserSwitchPin> UserSwitchPins => Set<UserSwitchPin>();

    public DbSet<Subaccount> Subaccounts => Set<Subaccount>();

    public DbSet<ServiceUser> ServiceUsers => Set<ServiceUser>();

    public DbSet<ServiceUserVersion> ServiceUserVersions => Set<ServiceUserVersion>();

    public DbSet<ContentAgeOption> ContentAgeOptions => Set<ContentAgeOption>();

    public DbSet<ContentLocale> ContentLocales => Set<ContentLocale>();

    public bool IncludeAllContentLocales { get; set; } = true;

    public string[] HiddenLocaleContentKeys { get; set; } = [];

    public string? SelectedContentLocale { get; set; }

    public bool ContentLocaleSpecified { get; set; }

    Task<string?> IContentLocaleStore.FindContentLocale(string entityType, Guid entityId, CancellationToken cancellationToken) =>
        this.FindContentLocale(entityType, entityId, cancellationToken);

    Task IContentLocaleStore.StageContentLocale(
        string entityType, Guid entityId, string? locale, CancellationToken cancellationToken) =>
        this.StageContentLocale(entityType, entityId, locale, cancellationToken);

    IQueryable<TEntity> IContentLocaleStore.InContentLocale<TEntity>(
        IQueryable<TEntity> source, string entityType, string? locale) =>
        this.InContentLocale(source, entityType, locale);

    DbContext IModuleContext.Context => this;

    IQueryable<TEntity> IModuleContext.InDeletionState<TEntity>(IQueryable<TEntity> source, DeletionState state) =>
        source.InDeletionState(this, DeletionRecorded, state);

    public SoftDeleteSwitch SoftDelete { get; } = new();

    public EntityMarkRegistry HardDeletes { get; } = new();

    public EntityMarkRegistry Restores { get; } = new();

    public IHardDeleteRepository<TAggregate, TKey> GetHardDeleteRepository<TAggregate, TKey>() =>
        (IHardDeleteRepository<TAggregate, TKey>)this;

    public IRestoreRepository<TAggregate, TKey> GetRestoreRepository<TAggregate, TKey>() =>
        (IRestoreRepository<TAggregate, TKey>)this;

    public IRepository<TAggregate, TKey> GetRepository<TAggregate, TKey>() => (IRepository<TAggregate, TKey>)this;

    public Task<DateTimeOffset?> DeletedAtAsync(
        string entityName, string entityKey, CancellationToken cancellationToken = default) =>
        AuditLogEntries
            .Where(audit => audit.EntityName == entityName && audit.EntityKey == entityKey
                            && audit.Action == new AuditLogEntry.Deleted())
            .OrderByDescending(audit => audit.Timestamp)
            .Select(audit => (DateTimeOffset?)audit.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> IsDeletedAsync(
        string entityName,
        string entityKey,
        CancellationToken cancellationToken = default)
    {
        var actions = await AuditLogEntries
            .Where(audit => audit.EntityName == entityName && audit.EntityKey == entityKey)
            .Select(audit => audit.Action)
            .ToListAsync(cancellationToken);

        return actions.Count(action => action is AuditLogEntry.Deleted)
               > actions.Count(action => action is AuditLogEntry.Restored);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbstractCoreContext).Assembly);
        modelBuilder.ApplyModuleConfigurations(Modules.Databases);
        modelBuilder.ApplyConfiguration(new ActiveVersionConfiguration());
        modelBuilder.Entity<ActiveVersion>().ToTable("ActiveVersions", table => table.ExcludeFromMigrations());
        UserConfiguration.ApplyHumanUser(modelBuilder);
        UserConfiguration.ApplyServiceUser(modelBuilder);
        UserConfiguration.ApplyServiceUserVersions(modelBuilder);
        UserConfiguration.ApplyUserRoles(modelBuilder);
        UserConfiguration.ApplyOidcPersistence(modelBuilder);
        UserConfiguration.ApplyUserAccess(modelBuilder);
        UserConfiguration.ApplyUserSettings(modelBuilder);
        UserConfiguration.ApplyUserApprovals(modelBuilder);
        RoleConfiguration.ApplyRoleGrants(modelBuilder);
        RoleConfiguration.ApplyRoleRestrictions(modelBuilder);
        MenuItemConfiguration.ApplyMenuItemRoles(modelBuilder);
        modelBuilder.ApplySoftDelete(
            this,
            DeletionRecorded,
            [.. CoreSoftDeletedEntities, .. Modules.SoftDeletedEntities]);
        modelBuilder.ApplyContentRestrictions(this,
        [
            typeof(Site), typeof(SiteSection), typeof(News), typeof(NewsCategory), typeof(NewsComment),
            typeof(Article), typeof(ArticleCategory), typeof(Gallery), typeof(GalleryItem),
            typeof(Carousel), typeof(CarouselSlide), typeof(DownloadFolder), typeof(Download), typeof(VfsFile),
            typeof(ChatMessage), ..Modules.ContentRestrictedEntities
        ], Modules.ContentRestrictionParentProperties);
        modelBuilder.ApplyContentLocales(this,
        [
            typeof(Site), typeof(SiteSection), typeof(News), typeof(NewsCategory), typeof(NewsComment),
            typeof(Article), typeof(ArticleCategory), typeof(Gallery), typeof(GalleryItem),
            typeof(Carousel), typeof(CarouselSlide), typeof(DownloadFolder), typeof(Download), typeof(VfsFile),
            typeof(ChatMessage), typeof(Menu), typeof(MenuItem), typeof(RouteAlias), typeof(Message),
            ..Modules.ContentRestrictedEntities
        ]);
        modelBuilder.Entity<Carousel>().HasAnnotation("Behind:LocaleOwnerType", nameof(Site));
    }

    private static readonly Type[] CoreSoftDeletedEntities =
    [
        typeof(ContentAgeOption),
        typeof(ContentRestriction),
        typeof(ContentLocale),
        typeof(User),
        typeof(Email),
        typeof(Role),
        typeof(Grant),
        typeof(Restriction),
        typeof(Password),
        typeof(UserSwitchPin),
        typeof(WallOfShameEntry),
        typeof(Site),
        typeof(News),
        typeof(NewsCategory),
        typeof(NewsComment),
        typeof(ChatMessage),
        typeof(Article),
        typeof(ArticleCategory),
        typeof(Gallery),
        typeof(Carousel),
        typeof(DownloadFolder),
        typeof(Download),
        typeof(Menu),
        typeof(MenuItem),
        typeof(RouteAlias),
        typeof(Message),
        typeof(Schedule),
        typeof(ScheduleRun),
        typeof(PrivateConversation),
        typeof(PrivateConversationRecipient),
        typeof(PrivateMessage),
        typeof(PrivateMailbox),
        typeof(PrivateMessageRead),
        typeof(PrivateUserBlock),
        typeof(UserReport),
        typeof(ReportEmailDelivery),
        typeof(PrivateMessageEmailDelivery),
        typeof(AccessSettings)
    ];

    public static readonly Expression<Func<AbstractCoreContext, string, string, bool>> DeletionRecorded =
        (context, entityName, entityKey) =>
            context.AuditLogEntries.Count(audit =>
                audit.EntityName == entityName && audit.EntityKey == entityKey && audit.Action == new AuditLogEntry.Deleted())
            > context.AuditLogEntries.Count(audit =>
                audit.EntityName == entityName && audit.EntityKey == entityKey && audit.Action == new AuditLogEntry.Restored());

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = Database.CurrentTransaction is null && ChangeTracker.HasChanges()
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        foreach (var entry in ChangeTracker.Entries<Subaccount>().Where(entry => entry.State == EntityState.Added))
        {
            var id = entry.VersionedId;
            entry.StampVersionedKey(id.Key, id.Version == 0 ? VersionedColumns.FirstVersion : id.Version);
        }

        foreach (var entry in ChangeTracker.Entries<ServiceUserVersion>().Where(entry => entry.State == EntityState.Added))
        {
            entry.StampVersionedKey(entry.Entity.Id.Key, entry.Entity.Id.Version);
        }

        if (ChangeTracker.Entries<ServiceUserVersion>().Any(entry => entry.State == EntityState.Modified))
        {
            throw new InvalidOperationException("Service-user versions are append-only.");
        }

        if (ChangeTracker.Entries<Subaccount>().Any(entry =>
                entry.State == EntityState.Modified
                || entry.State == EntityState.Deleted && !HardDeletes.IsMarked(entry.Entity)))
        {
            throw new InvalidOperationException("Subaccount versions are append-only.");
        }

        foreach (var entry in ChangeTracker.Entries<User>().Where(entry =>
                     entry.State == EntityState.Deleted && HardDeletes.IsMarked(entry.Entity)))
        {
            var id = entry.Property<Guid>("Id").CurrentValue;
            await LockUserRowForAuditAsync(id, cancellationToken);
            if (await this.IsUserReferencedByHistoryAsync(id, cancellationToken))
            {
                throw new DbUpdateException("A user referenced by audit entries cannot be permanently deleted.");
            }
        }

        await LockAuditReferencesAsync(cancellationToken);
        await this.ExpandCascadingDeletesAsync(cancellationToken);
        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        HardDeletes.Clear();
        Restores.Clear();
        return saved;
    }

    public async Task LockAuditReferencesAsync(CancellationToken cancellationToken)
    {
        var ids = ChangeTracker.Entries<AuditLogEntry>()
            .Where(entry => entry.State == EntityState.Added)
            .SelectMany(entry => new[]
            {
                entry.Property<Guid?>("UserId").CurrentValue,
                entry.Property<Guid?>("BehalfOfId").CurrentValue
            })
            .OfType<Guid>()
            .Distinct()
            .Order()
            .ToList();

        foreach (var id in ids)
        {
            await LockUserRowForAuditAsync(id, cancellationToken);
        }
    }

    private async Task LockUserRowForAuditAsync(Guid id, CancellationToken cancellationToken)
    {
        var activeVersions = Set<ActiveVersion>();
        var entityKey = id.ToString();
        var serviceUserLocked = await activeVersions
            .Where(active => active.EntityType == nameof(ServiceUser) && active.EntityKey == entityKey)
            .ExecuteUpdateAsync(setters => setters.SetProperty(active => active.Version, active => active.Version), cancellationToken);
        if (serviceUserLocked == 1)
        {
            return;
        }

        var humanUserLocked = await Users.IgnoreQueryFilters()
            .Where(user => EF.Property<Guid>(user, "Id") == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.HideEmail, user => user.HideEmail), cancellationToken);
        if (humanUserLocked == 0)
        {
            throw new DbUpdateException("An audit entry cannot reference a user that no longer exists.");
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Deletions must be saved with SaveChangesAsync.");

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, ModuleModelCacheKeyFactory>();

        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(
                new EventId(20609),
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)
        );
    }
}
