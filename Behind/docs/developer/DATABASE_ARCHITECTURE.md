# Database Architecture

This document covers the database layer design, Entity Framework Core patterns, repository implementation, and data access strategies.

## Overview

Behind uses **Entity Framework Core 10.0.10** as the ORM with support for multiple database backends:

- **SQLite 3.53.3** - Default for development (SQLitePCLRaw 2.1.12)
- **PostgreSQL** - Via Aspire orchestration for advanced deployments
- **Other databases** - Potentially supported through EF Core providers

## DbContext Architecture

### DbContext Structure

The Behind.Core.Database project contains the DbContext and entity configurations:

```
Behind.Core.Database/
├── DbContextExtensions.cs          # DbSet extension methods
├── FilterExtensions.cs             # Filter application to IQueryable
├── ApplicationDbContext.cs          # Main DbContext
├── EntityConfigurations/           # Fluent API configurations
│   ├── UserConfiguration.cs
│   ├── RoleConfiguration.cs
│   └── ...
└── Migrations/                     # EF Migrations
    ├── [timestamp]_InitialMigration.cs
    └── ...
```

### DbContext Implementation

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // Aggregates as DbSets
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Grant> Grants { get; set; }
    public DbSet<Session> Sessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new GrantConfiguration());
        modelBuilder.ApplyConfiguration(new SessionConfiguration());
    }

    // Soft delete support
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDeletable);

        foreach (var entry in entries)
        {
            entry.State = EntityState.Modified;
            ((ISoftDeletable)entry.Entity).DeletedAt = DateTime.UtcNow;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

## Entity Configuration

### Entity Type Mapping

Use **Fluent API** for entity configurations to keep DbContext clean:

```csharp
namespace Behind.Core.Database.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasValueGenerator<GuidValueGenerator>();

        // Discriminator for inheritance hierarchy
        builder.HasDiscriminator<string>("UserType")
            .HasValue<HumanUser>("HumanUser")
            .HasValue<SystemUser>("SystemUser")
            .HasValue<GuestUser>("GuestUser")
            .HasValue<BannedUser>("BannedUser");

        // Human user specific
        builder.OwnsMany(u => (u as HumanUser)!.Emails, navigationBuilder =>
        {
            navigationBuilder.ToJson();  // Store as JSON column
            navigationBuilder.WithOwner();
            navigationBuilder.HasKey("Id");
            navigationBuilder.Property(e => e.Value)
                .HasMaxLength(255)
                .IsRequired();
        });

        // Relationships
        builder.HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity("UserRole");

        // Concurrency token
        builder.Property(u => u.ConcurrencyToken)
            .IsConcurrencyToken();

        // Index for common queries
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.CreatedAt);
    }
}
```

### Value Object Mapping

```csharp
// Email value object stored as JSON
public class EmailConfiguration : IEntityTypeConfiguration<Email>
{
    public void Configure(EntityTypeBuilder<Email> builder)
    {
        builder.Property(e => e.Value)
            .HasConversion(v => v.Value, v => new Email(v))
            .HasMaxLength(255);
    }
}

// Or use owned entities
builder.OwnsOne(u => u.Email, navigationBuilder =>
{
    navigationBuilder.Property(e => e.Value)
        .HasColumnName("Email")
        .HasMaxLength(255)
        .IsRequired();
});
```

### Inheritance Strategies

Behind uses **Table-Per-Hierarchy (TPH)** for the User aggregate:

```csharp
// In User aggregate
public abstract record User(Option<IEnumerable<Role>> Roles)
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record HumanUser : User { }
public sealed record SystemUser : User { }
public sealed record GuestUser : User { }
public sealed record BannedUser : User { }

// Single Users table with UserType discriminator column
// Advantages:
// - Simple queries
// - Easy filtering
// - Good for known, limited hierarchy
//
// Disadvantages:
// - Nullable columns for specific types
// - Single table can grow large
```

**When to use TPH vs TPT vs TPC**:
- **TPH** - Few types, similar queries ✓ Used in Behind
- **TPT** - Many specialized types, specific queries
- **TPC** - Very different types, rarely queried together

## Repository Implementation

### Generic Repository Interface

Defined in `Behind.Common.Database`:

```csharp
public interface IRepository<TAggregate, TKey>
{
    TKey GetKey(TAggregate aggregate);
    
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
    
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    
    void Update(TAggregate aggregate);
    
    void Update(TKey key, TAggregate aggregate);
    
    void Delete(TAggregate aggregate);

    public async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var aggregate = await TryFindAsync(key, cancellationToken);
        aggregate.Match(() => { }, Delete);
    }
}

public interface IRepository<T> : IRepository<T, Guid> where T : class;
```

### Repository Implementation Pattern

```csharp
public class UserRepository : IRepository<User>
{
    private readonly DbSet<User> dbSet;
    private readonly ApplicationDbContext dbContext;
    private readonly string[] includes;

    public UserRepository(
        ApplicationDbContext dbContext,
        params string[] includes)
    {
        this.dbContext = dbContext;
        this.dbSet = dbContext.Set<User>();
        this.includes = includes;
    }

    public Guid GetKey(User user) => user.Id;

    public async Task<Option<User>> TryFindAsync(
        Guid key, 
        CancellationToken cancellationToken = default)
    {
        var query = dbSet.AsQueryable();

        // Apply lazy-loaded includes
        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        var user = await query
            .FirstOrDefaultAsync(u => u.Id == key, cancellationToken);

        return user;
    }

    public async Task AddAsync(
        User user, 
        CancellationToken cancellationToken = default)
    {
        await dbSet.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Update(User user)
    {
        dbSet.Update(user);
        dbContext.SaveChanges();
    }

    public void Delete(User user)
    {
        dbSet.Remove(user);
        dbContext.SaveChanges();
    }

    // Domain-specific queries
    public async Task<Option<User>> TryFindByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        var user = await dbSet
            .FirstOrDefaultAsync(u => u.Emails.Contains(email), cancellationToken);

        return user;
    }

    public async Task<IEnumerable<User>> FindByRoleAsync(
        Role role,
        CancellationToken cancellationToken = default)
    {
        return await dbSet
            .Where(u => u.Roles.Match(
                () => false,
                roles => roles.Contains(role)
            ))
            .ToListAsync(cancellationToken);
    }
}
```

### Immutable Entity Repository

For read-only data access:

```csharp
public interface IImmutableEntityRepository<TEntity> where TEntity : class
{
    Task<Option<TEntity>> TryFindAsync(object key, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> FindAsync(Filter<TEntity> filter, CancellationToken cancellationToken = default);
    Task<PagedResult<TEntity>> FindPagedAsync(Filter<TEntity> filter, int page, int pageSize, CancellationToken cancellationToken = default);
}

public class ImmutableEntityRepository<TEntity> : IImmutableEntityRepository<TEntity> where TEntity : class
{
    private readonly DbContext dbContext;
    private readonly DbSet<TEntity> dbSet;
    private readonly string[] includes;

    public ImmutableEntityRepository(
        DbContext dbContext,
        DbSet<TEntity> dbSet,
        params string[] includes)
    {
        this.dbContext = dbContext;
        this.dbSet = dbSet;
        this.includes = includes;
    }

    public async Task<Option<TEntity>> TryFindAsync(
        object key,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbSet.FindAsync(new object[] { key }, cancellationToken);
        return entity;
    }

    public async Task<IEnumerable<TEntity>> FindAsync(
        Filter<TEntity> filter,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyIncludes(dbSet.AsQueryable());
        return await filter.Apply(query).ToListAsync(cancellationToken);
    }

    private IQueryable<TEntity> ApplyIncludes(IQueryable<TEntity> query)
    {
        return includes.Aggregate(query, (q, include) => q.Include(include));
    }
}
```

**Usage**:
```csharp
var repo = dbSet.ToImmutableEntityRepository(dbContext, "Roles", "Permissions");
var user = await repo.TryFindAsync(userId);
var allUsers = await repo.FindAsync(new Filter<User>(u => u.Active));
```

## Unit of Work Pattern

### IUnitOfWork Interface

```csharp
public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<User> Users { get; }
    IRepository<Role> Roles { get; }
    IRepository<Grant> Grants { get; }
    IRepository<Session> Sessions { get; }
    IRepository<AuditLogEntry> AuditLogs { get; }

    Task CommitAsync(CancellationToken cancellationToken = default);
    
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

### Implementation

```csharp
public class ApplicationUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext dbContext;
    private IRepository<User>? users;
    private IRepository<Role>? roles;
    private IRepository<Grant>? grants;
    private IRepository<Session>? sessions;
    private IRepository<AuditLogEntry>? auditLogs;

    public ApplicationUnitOfWork(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public IRepository<User> Users =>
        users ??= new UserRepository(dbContext, "Roles", "Permissions");

    public IRepository<Role> Roles =>
        roles ??= new RoleRepository(dbContext);

    public IRepository<Grant> Grants =>
        grants ??= new GrantRepository(dbContext);

    public IRepository<Session> Sessions =>
        sessions ??= new SessionRepository(dbContext);

    public IRepository<AuditLogEntry> AuditLogs =>
        auditLogs ??= new AuditLogRepository(dbContext);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.RollbackTransactionAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
    }
}
```

### Using Unit of Work

```csharp
public class UserRegistrationService
{
    private readonly IUnitOfWork unitOfWork;

    public async Task<Exceptional<User>> RegisterUserAsync(RegisterDto dto)
    {
        try
        {
            // Load related data
            var defaultRole = await unitOfWork.Roles
                .TryFindAsync(Guid.Parse(DomainConstants.DefaultRoleId));

            // Create aggregate
            var user = new HumanUser(
                displayName: dto.DisplayName,
                emails: new[] { new Email(dto.Email) },
                roles: defaultRole.AsEnumerable()
            );

            // Persist through unit of work
            await unitOfWork.Users.AddAsync(user);
            
            // Create audit log
            var auditLog = new AuditLogEntry(
                EntityName: "User",
                Operation: "Created",
                EntityId: user.Id,
                Changes: "New user registered"
            );
            await unitOfWork.AuditLogs.AddAsync(auditLog);

            // Commit all changes atomically
            await unitOfWork.CommitAsync();

            return user;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync();
            return ex;
        }
    }
}
```

## Querying Patterns

### Filter/Specification Pattern

```csharp
// Generic filter encapsulates query logic
public record Filter<T>(Expression<Func<T, bool>> Predicate)
{
    public IQueryable<T> Apply(IQueryable<T> queryable) => queryable.Where(Predicate);
}

// Domain-specific specifications
public static class UserFilters
{
    public static Filter<User> ByEmail(Email email) =>
        new(u => u.Emails.Contains(email));

    public static Filter<User> ByRole(Role role) =>
        new(u => u.Roles.Match(
            () => false,
            roles => roles.Contains(role)
        ));

    public static Filter<User> Active =>
        new(u => !(u is BannedUser));

    public static Filter<User> CreatedAfter(DateTime date) =>
        new(u => u.CreatedAt > date);
}

// Composition
public async Task<Option<User>> FindActiveAdminByEmailAsync(
    Email email,
    IRepository<User> repository,
    IRepository<Role> roleRepository)
{
    var adminRole = await roleRepository.TryFindAsync(Guid.Parse(DomainConstants.AdminRoleId));

    return await adminRole.Match(
        async () => None,
        async role =>
        {
            var filter = new Filter<User>(u =>
                u.Emails.Contains(email) &&
                u.Roles.Match(
                    () => false,
                    roles => roles.Contains(role)
                ) &&
                !(u is BannedUser)
            );

            return await repository.TryFindAsync(/* ... */);
        }
    );
}
```

### Pagination

```csharp
public record PagedResult<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }
}

// Usage
public async Task<PagedResult<UserDto>> GetUsersAsync(int page = 1, int pageSize = 20)
{
    var filter = UserFilters.Active;
    
    var pagedResult = await dbSet
        .Where(filter.Predicate)
        .OrderBy(u => u.CreatedAt)
        .ToPagedResultAsync(page, pageSize);

    return pagedResult with
    {
        Items = pagedResult.Items.Select(MapToDto)
    };
}
```

## Automatic Audit Logging

Every `SaveChangesAsync` on the core context writes `AuditLogEntry` rows for all non-audit entities in the same transaction, through `AuditSaveChangesInterceptor` (Behind.Core.Database/Auditing):

- **Created / Deleted** for added and removed entities, **Updated** with the changed properties (old and new values read from the database) for modified ones. Updates without real changes are not logged.
- `EntityName` is the root entity type (`User`, `Role`, `Email`, `ValidSession`, ...) and `EntityKey` is the primary key.
- Properties marked `[Sensible]` (password hash, token nonces) are stored as `***`.
- The actor (`UserId` / `BehalfOfId`) comes from `IAuditActorProvider`: `HttpAuditActorProvider` resolves it from the session claim of the current request, anonymous requests are logged without a user and code running outside a request is logged as the system user.
- Impersonation: a session started with `ISessionCommands.CreateImpersonation` stores the impersonator in its `ImpersonatorId` column. `ISessionQueries.TryFindActor` returns the session user and the impersonator, and `AuditActor.ForSession` turns that into the actor: the impersonator is the `UserId` and the impersonated user is the `BehalfOfId`. Creating such a session is audited as `ImpersonationStarted` (on behalf of the target) and invalidating it as `ImpersonationEnded` instead of `Created` / `Updated`.
- `AuditLogEntry` rows are never audited themselves and synchronous `SaveChanges` is not supported.
- The system and guest users are seeded by the migrator (`EnsureBuiltInUsersAsync`), and the `UserId` / `BehalfOfId` keys of audit rows are `NoAction`, so a user referenced by audit rows can only be soft deleted; a hard delete is rejected by the database to keep the audit trail attributable.

Do not add `AuditLogEntry` rows manually for these entities.

## Soft Delete, Hard Delete and Deleted Records Visibility

There is no `IsDeleted` column. A record is deleted when it has more `Deleted` than `Restored` `AuditLogEntry` rows for its `EntityName` and `EntityKey`.

- `User`, `Email`, `Role`, `Grant`, `Password` and `WallOfShameEntry` are soft deletable (`ApplySoftDelete` in `AbstractCoreContext`). Removing them keeps the row, writes the `Deleted` audit entry and a global query filter (`SoftDelete`) hides them. Cascading dependents are deleted the same way, dependents that are not soft deletable (sessions) are removed physically.
- Hard delete is explicit: `IUnitOfWork.GetHardDeleteRepository<TAggregate, TKey>()` returns `IHardDeleteRepository` with `HardDelete` / `HardDeleteAsync`. The row (and its cascading dependents) is removed permanently and the audit log gets a `HardDeleted` entry for each of them, so the event stays traceable. Hard deleting an already soft deleted record is supported.
- Restore is explicit: `IUnitOfWork.GetRestoreRepository<TAggregate, TKey>()` returns `IRestoreRepository` with `RestoreAsync`. The record and its soft deleted cascading dependents are marked (`ISoftDeleteContext.Restores`) and the next `SaveChangesAsync` writes a `Restored` audit entry for each of them, the earlier `Deleted` entries are kept. Restoring a record that is not deleted does nothing.
- Soft delete relies on `AuditSaveChangesInterceptor`; a context without it removes rows physically.
- `context.SoftDelete.IncludeDeleted()` returns an `IDisposable` scope (`SoftDeleteSwitch`, thread safe and nestable) that disables the filter on that context while it is open. `AuditLogEntryQueries` uses it so audit entries always resolve their users.
- When a stored user id (audit `UserId` / `BehalfOfId`, wall of shame `UserId` / `GivenById`, session `InvalidatedById`) cannot be resolved to a user, for example because the user is soft deleted and the filter is active, the entity is populated with `UnknownUser(Id)` instead of failing (`UserResolutionExtensions.ToUser`).
- `DeletedRecordsVisibilityMiddleware` opens the scope for the whole request of authenticated users holding the `ViewDeleted` grant (`DomainConstants.ViewDeletedGrant`, checked with `IUserQueries.HasGrant`), so those admins see deleted records in every query of that request.

### Management lists

Every management page (users, roles, sites) follows the same shape: a paged list with a search term, a first letter bucket and a deletion state, `Delete` (soft), and for developers (`RawDatabase.Manage`) `deleted=true` listing, `restore` and `hard/delete`.

- `InDeletionState` (`Behind.Common.Database`) filters a query on whether the audit log says the row is deleted, even inside an `IncludeDeleted` scope. Soft delete keys are the primary key values joined with `:` (the same text as the audit `EntityKey`), so entities with a composite key such as `Site` (`SiteId`, `Version`) can be soft deleted, every row (version) gets its own `Deleted` / `Restored` entry.
- `InBucket` / `LettersAsync` (`LetterBucketQueryExtensions`) implement the letter filter for any query, given the label expression.
- Hard delete commands answer `HardDeleteResult` (`NotFound` when the record is not soft deleted, `Rejected` when the database refuses it, mapped to `404` / `409`).
- Deleting a site soft deletes its `Sites` rows only. The `ActiveVersions` row stays, so a restore brings the live version back, and a hard delete removes it too.

### Users management

- `POST /users/{id}/delete` soft deletes a user (`User.Delete` grant, same moderation rules as ban: not yourself, only developers may delete developers). The user's sessions are removed with it.
- `GET /users?deleted=true`, `POST /users/{id}/restore` and `POST /users/{id}/hard/delete` are developer only. Restore and hard delete only apply to users that are already soft deleted (otherwise `404`), so a permanent delete is always a second, explicit step.
- A hard delete answers `409` when the database rejects it, for example because audit entries are attributed to the user (see the audit log notes above).
- `IUserManagementQueries` takes a `DeletionState` (`Active` / `Deleted`) and opens the `IncludeDeleted` scope itself for `Deleted`; `Active` always excludes deleted users, even inside a `DeletedRecordsVisibilityMiddleware` scope.

## Versioned Sites

`Site` is the first versioned entity. Its identity is a `VersionedKey<Guid>` (`Behind.Common`, next to `ActiveVersion` in `Behind.Common.Database`) (the site id and a version number starting at 1) and the version that visitors see is decided by an `ActiveVersion` row, never by the site row itself.

- `Sites` has the composite key (`SiteId`, `Version`). The domain `Site.Id` is ignored by EF and stored in the two shadow columns, like the other entities keep their ids. A save never updates a row: `ISiteCommands.Create` / `SaveVersion` insert version `latest + 1` (a concurrent insert of the same version fails on the key and returns `SiteSaveResult.VersionConflict`).
- `PublishedAt` (`Option<DateTimeOffset>`) is the draft flag: `None` is a draft. It is stored in the shadow column `PublishedAtUtc`. The only update ever made to a `Sites` row is stamping `PublishedAtUtc` when a draft is made live with `ISiteCommands.Activate`.
- `ActiveVersions` (`EntityType`, `EntityKey`, `Version`, key `EntityType` + `EntityKey`) holds the live version, `EntityType` is `Site.EntityType` and `EntityKey` the site id. It is a general table, so it has its own context: `AbstractVersioningContext` (`VersioningContextSqlite`) with its own migrations in `Migrations/Versioning` and its own history table (`__VersioningMigrationsHistory`). `ActiveVersion`, its mapping and `ActiveVersionsAsync` / `SetActiveVersionAsync` live in `Behind.Common.Database`. The migrator applies the core migrations first and the versioning ones second.
- The versioning context is audited like the core one (same `AuditSaveChangesInterceptor`): it maps `AuditLogEntry` onto the existing `AuditLogEntries` table with `ExcludeFromMigrations`, so its migrations never create or change that table. Publishing or activating adds the `ActiveVersions` row or replaces it through `UpdateImmutable`, so the audit log shows an `Updated` entry with the `Version` change.
- The two contexts are saved one after the other (no shared transaction): `SiteCommands` saves the `Sites` row first and the `ActiveVersions` row second, so a failure in between leaves a published version that is not live yet, never a live version that does not exist.
- A slug is taken when another site has it in its live or its newest version (`IsSiteSlugTakenAsync`), which is checked on save and on activation (`SiteSaveResult.SlugTaken`). `ISiteQueries.FindActiveBySlug` resolves the slug of the live versions only, drafts never leak.
- `SiteImages` stores uploaded images (`FileName`, sniffed `ContentType`, `Content` blob) so the images travel with the database. They are served by `GET /sites/images/{id}` with an immutable cache header.
- Audit labels: `AuditLabelExtensions` names a site by the title of its newest version (`Site` keys are `<id>:<version>`) and an image by its file name.
- Content is sanitized with `SanitizeRichText()` (`Behind.Common.WebApi`, HtmlSanitizer) before it is saved; only the editor's tags, `class` values and `http` / `https` / `mailto` links are kept.

The `Site` grant area (`Site.Read`, `Write`, `Delete`, `Manage`) is administration only: `Users` do not get `Site.Read`.

## News

`News`, `NewsCategory` and `NewsComment` are versioned entities like `Site`: their `Id` is a `VersionedKey<Guid>` (`Behind.Common`), stored as the shadow composite key (`Key`, `Version`) by `HasVersionedKey()` (`Behind.Common.Database.VersionedEntityExtensions`), mapped to `News`, `NewsCategories` and `NewsComments`. A save inserts version `latest + 1` and never updates a row (the only in-place change is clearing the highlight, see below). All three are soft deletable roots, every version row gets its own `Deleted` / `Restored` audit entry (`<key>:<version>`). The shared helpers live in `Behind.Common.Database`: `OfKey`, `OfKeys`, `OfVersion`, `LatestOf` (the highest version of each key), `LatestVersionAsync`, `StampVersionedKey` / `VersionedId`, and `MarkSoftDeletedAsync` / `MarkRestoredAsync` / `MarkHardDeletedAsync` / `HasDeletedRowsAsync` that apply a lifecycle change to every version of a key.

- **Which version is shown.** `News` works exactly like `Site`: a version is a draft until it is made live. `ActiveVersions` (`EntityType` = `News.EntityType`, `EntityKey` = the news key) points at the live version, saved through `AbstractVersioningContext` (`NewsCommands.Create` / `SaveVersion` with `publish`, `Activate`). `AbstractCoreContext` also maps `ActiveVersion` read-only (`ExcludeFromMigrations`), so core queries can join the live version in SQL (`LiveIn`) while paging. `NewsCategory` and `NewsComment` have no draft: the latest version is the current one.
- **No foreign keys between versioned entities.** A foreign key cannot target a composite key, so `News.CategoryId` and `NewsComment.NewsId` are plain key columns (the author and the header image keep real foreign keys). The commands do the cascades: deleting, restoring or hard deleting a news applies to the news versions and to the comments of the news. A deleted category keeps its news, which are shown without a category because the queries only attach active categories.
- `News` (`Title`, `Summary`, `Content`, `StartAt`, `Highlighted`) keeps `AuthorId` (the author of version 1 is kept by every later version), the optional `CategoryId` and the optional end (`EndAt`, shadow column `EndAtUtc`, like `Site.PublishedAt`). `Summary` is the short content shown in the news list, `Content` is shown when the news is opened, both are sanitized with `SanitizeRichText()`. When the content is empty the opened news shows the summary (`NewsDetail.DisplayContent`, `displayContent` in the response) while `content` stays untouched for the editor.
- **Created at and updated at come from the audit log** (rule in `AGENTS.md`): `AuditTimeExtensions.AuditTimesAsync<T>` takes the shown version of each key and returns the `Created` entry time of version 1 as the created at and, when the shown version is later than 1, the `Created` entry time of the shown version as the updated at. `VersionCreationTimesAsync` gives the time of each version (the version list of the editor) and `OrderByCreation` / `ThenByCreation` order a query by the creation of version 1 in SQL. Rows written through a context without the audit interceptor have no times.
- **Schedule (optional).** A version has no schedule by default (no start and no end: shown as soon as it is live, until it is deleted). A schedule has a required `StartAt` and an optional `EndAt`, both `Option<DateTimeOffset>` stored in the nullable shadow columns `StartAtUtc` / `EndAtUtc` (like `Site.PublishedAt`). A news is shown while its live version has no start or `StartAt <= now`, and no end or `EndAt > now` (`NewsScheduleExtensions.StatusAt`, `ActiveAt`). An end without a start, or an end that is not after the start, is rejected (`NewsSaveResult.InvalidSchedule`, `409` `invalid-schedule`, `IsValidSchedule`). The public list, the public `Find(id, now)`, the comment list and `NewsCommentCommands.Add` (which needs the `TimeProvider`) only see live news. `ListManaged`, `FindHistory` and `FindVersion` have no time or live filter. The lists are ordered by the start time when there is one and by the creation time otherwise (`OrderByCreation(..., overriddenBy)` coalesces the two in SQL), the public list puts the highlighted news first.
- **Highlight.** Only one news can be highlighted: the highlight of a news is the `Highlighted` flag of its live version. Publishing or activating a highlighted version clears the flag of the other live highlighted versions (an in-place update of those rows, audited, no new version), publishing a not highlighted version ends the highlight of that news, and `Delete` clears the flag first so a deleted news never holds the highlight and a restore cannot bring back a second one. There is no database constraint for it, the commands keep the rule.
- `NewsCategory` (`Name`) keeps the optional `HeaderImageId` (`SetNull` to `SiteImages`). Names are unique among the active categories ignoring case (`NewsCategorySaveResult.NameTaken`) and restoring a category whose name was taken meanwhile is refused.
- `NewsComment` (`Content`, up to 2000 characters) keeps `NewsId` and `AuthorId`. Editing a comment (`INewsCommentCommands.Update`) inserts the next version with the same news and author, so the edit history is kept, and the "edited" time is the creation time of the shown version. Who may edit is decided by `NewsCommentEndpoints`: the author, or a holder of `News.Manage` (`EditGrant`), a moderator role in the future only has to be given that grant.
- The public queries (`INewsQueries.List` / `Find`, `INewsCommentQueries`, `INewsCategoryQueries.ListActive`) only read active (not soft deleted) rows, so developers, whose requests include deleted records, never see deleted rows on the public pages. Authors, categories and comment counts are resolved in one query per page.
- Header and content images reuse `SiteImages` (`POST /news/images` is `SiteImageEndpoints.Upload` behind the `News.Write` grant, images are served by `GET /sites/images/{id}`).
- Audit labels: `AuditLabelExtensions` names a news by the title of its newest version, a category by its name and a comment as `News comment of <author>`; the audit keys are `<id>:<version>`, so `LabelKey` shows `Title: 2`.
- Migrations: `MakeNewsVersioned` renames `Id` to `Key`, adds `Version` (existing rows become version 1), drops the foreign keys between the news tables and rewrites the audit keys to `<id>:1`; `PublishExistingNews` (versioning context) makes the existing news live.

The `News` grant area (`News.Read`, `Write`, `Delete`, `Manage`) is administration only: `Users` do not get `News.Read`. Reading the news is public, commenting needs a signed in session, deleting a comment needs to be its author or to hold `News.Delete`.

## Articles

`Article` and `ArticleCategory` are versioned entities exactly like `News` and `NewsCategory` (`VersionedKey<Guid>` id, drafts, `ActiveVersions` with `Article.EntityType`, soft delete, hard delete, restore), with these differences: an article has only `Title`, `Summary` and `Content` (plus the shadow `AuthorId` and `CategoryId`), no schedule, no highlight and no comments, and it has its own categories (`ArticleCategory`, `Name`, optional `HeaderImageId`) which are independent from the news categories.

- Search: `ArticleQueryExtensions.Matching` filters on the lower cased title or content (`IArticleQueries.List` searches the live versions, `ListManaged` and `GetLetters` the latest versions).
- Audit labels: `AuditLabelExtensions` names an article by the title of its newest version and an article category by its name.
- Migration: `AddArticles` creates `Articles` and `ArticleCategories`.

The `Article` grant area (`Article.Read`, `Write`, `Delete`, `Manage`) is administration only, like `News`: `Users` do not get `Article.Read`. Reading the articles is public.

## Migrations

### Creating Migrations

```bash
cd Behind/src/Core/Behind.Core.Database.Migrator

# Create a new migration
dotnet ef migrations add "AddUserTable" \
    -c ApplicationDbContext \
    -o Migrations

# Apply migrations
dotnet ef database update -c ApplicationDbContext

# Remove last migration
dotnet ef migrations remove -c ApplicationDbContext
```

### Migration Best Practices

```csharp
public partial class AddUserTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserType = table.Column<string>(type: "TEXT", nullable: false),
                Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                ConcurrencyToken = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Users");
    }
}
```

**Guidelines**:
- One logical change per migration
- Descriptive migration names
- Reversible Down() method
- Include indexes for queryable columns
- Add constraints for data integrity

## Transaction Management

### Explicit Transactions

```csharp
public async Task<Exceptional<TransferResult>> TransferFundsAsync(
    Guid fromUserId,
    Guid toUserId,
    decimal amount,
    CancellationToken cancellationToken = default)
{
    using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
    {
        try
        {
            var fromUser = await users.TryFindAsync(fromUserId, cancellationToken);
            var toUser = await users.TryFindAsync(toUserId, cancellationToken);

            return await (
                from from_u in fromUser
                from to_u in toUser
                select ValidateTransfer(from_u, to_u, amount) into validation
                select await validation.Match(
                    errors => throw new ValidationException(errors),
                    valid => ExecuteTransferAsync(valid, cancellationToken)
                )
            ).RunAsync();

            await transaction.CommitAsync(cancellationToken);
            return transferResult;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ex;
        }
    }
}
```

### SavePointspoint Support

```csharp
public async Task<Exceptional<User>> RegisterWithAuditAsync(
    RegisterDto dto,
    CancellationToken cancellationToken = default)
{
    await using (var transaction = await dbContext.Database.BeginTransactionAsync())
    {
        try
        {
            // Create user
            var user = new HumanUser(dto.DisplayName, new[] { new Email(dto.Email) }, None);
            await users.AddAsync(user, cancellationToken);

            // Savepoint before audit
            await transaction.CreateSavepointAsync("BeforeAudit", cancellationToken);

            // Create audit (might fail)
            var auditLog = CreateAuditLog(user);
            await auditLogs.AddAsync(auditLog, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return user;
        }
        catch (Exception ex) when (ex is AuditException)
        {
            // Rollback to savepoint, keep user
            await transaction.RollbackToSavepointAsync("BeforeAudit", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return user;  // User created without audit
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ex;
        }
    }
}
```

## Performance Considerations

### N+1 Query Problem

**Bad** ✗
```csharp
var users = dbSet.ToList();
foreach (var user in users)
{
    var roles = dbSet.Entry(user)
        .Collection(u => u.Roles)
        .Query()
        .ToList();  // Separate query per user!
}
```

**Good** ✓
```csharp
var users = await dbSet
    .Include(u => u.Roles)
    .ThenInclude(r => r.Permissions)
    .ToListAsync();  // Single query with joins
```

### Query Projection

**Bad** ✗
```csharp
var users = await dbSet.ToListAsync();  // Load entire entities
var userDtos = users.Select(MapToDto).ToList();  // Map in memory
```

**Good** ✓
```csharp
var userDtos = await dbSet
    .Select(u => new UserDto
    {
        Id = u.Id,
        Email = u.Email.Value,
        DisplayName = (u as HumanUser)!.DisplayName
    })
    .ToListAsync();  // Projection pushed to database
```

### Indexing Strategy

```csharp
// Primary lookups
builder.HasIndex(u => u.Id).IsUnique();

// Foreign keys
builder.HasIndex(u => u.RoleId);

// Filtering
builder.HasIndex(u => u.CreatedAt);
builder.HasIndex(u => u.IsActive);

// Composite indexes
builder.HasIndex(u => new { u.IsActive, u.CreatedAt });
```

## Database Connectivity

### Connection Strings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db",  // SQLite
    "PostgreSQL": "Host=localhost;Port=5432;Database=behind;User=postgres;Password=password"
  }
}
```

### DbContext Registration

```csharp
extension(IServiceCollection)
{
    public IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseSqlite(connectionString)
                .EnableSensitiveDataLogging()  // Development only
                .LogTo(Console.WriteLine)      // Development only
        );

        services.AddScoped<IUnitOfWork, ApplicationUnitOfWork>();
        services.AddScoped<IRepository<User>, UserRepository>();
        services.AddScoped<IRepository<Role>, RoleRepository>();

        return services;
    }
}
```

## Summary

| Pattern | Purpose | Location |
|---------|---------|----------|
| DbContext | ORM coordination | Behind.Core.Database |
| IEntityTypeConfiguration | Fluent mappings | Behind.Core.Database |
| IRepository<T> | Data access abstraction | Behind.Common.Database |
| IUnitOfWork | Transaction coordination | Behind.Core.Database |
| Filter<T> | Query specifications | Behind.Common |
| Migrations | Schema versioning | Behind.Core.Database.Migrator |

---

**Key Takeaways**:
- Use repository pattern for data access abstraction
- Leverage Unit of Work for multi-repository transactions
- Use filters/specifications for reusable query logic
- Configure entities with Fluent API
- Manage migrations carefully
- Optimize queries to avoid N+1 problems
