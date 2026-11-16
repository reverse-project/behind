# Database Architecture

## Private messaging and report persistence

Private messaging/report tables use Core's existing migration chain in the shared SQLite/PostgreSQL database. Apply migrations with the installation migrator before starting the host. New entities use `(Key, Version)` keys and audit timestamps. Mailbox state is per user; deletion does not remove other participants' history.

`UserReports` stores reason/evidence snapshots and decisions. `ReportEmailDeliveries` preserves delivery destinations and status for retries. Report management supports soft delete and developer restore/permanent deletion. Raw database discovery includes the tables, masks sensitive content and audit labels avoid exposing private subjects or evidence.

`PrivateMessageEmailDeliveries` is a versioned notification outbox added by `AddPrivateMessageEmails` on both providers. Saving a new message and its recipient-address snapshots is atomic. Delivery durably claims a pending/failed record as sending before SMTP, then appends sent or failed. Known SMTP rejections retry up to three attempts; uncertain sending is never automatically retried. Reference IDs and email addresses are sensitive in raw editing, with non-disclosing audit labels and audit-derived timestamps.

## Contact persistence

`IContactContext` adds versioned contact forms and requests to the main installation's single SQLite/PostgreSQL database. Contact migrations have their own `__contactMigrationsHistory` table and run through the web project migrator after Core. Apply migrations before starting the updated host.

Common.Database's `ModuleModelCacheKeyFactory` keys EF models by context type, the installed catalog exposed through `IModuleModelContext`, and design-time state. `AbstractCoreContext` registers this factory so contexts with different installed modules cannot reuse an incompatible model.

Both entity types use append-only `(Key, Version)` identities, audit-derived creation/update times, audit deletion state, soft delete and developer restore/permanent deletion. Module audit labels and raw database discovery include both entities; personal submission data and recipient settings are sensitive fields.

A request snapshots its form name, destination, subject and submitted field labels/values. Saving it precedes the SMTP attempt, so delivery failure does not discard the inbox record. Delivery and shared read state are versioned changes; editing a form does not alter existing submissions or their retry destination. Deleting an inbox record cannot retract an already-delivered email.

## Content locale persistence

Optional content-locale assignments are persisted with the shared database and participate in public content filtering across installed modules. The unlocalized content space remains the default for existing records and is shared with selected locales; no migration assigns the interface's default language to legacy content. Site and route-alias slug checks include shared slugs when checking a selected locale. Apply the updated Core migrations through the installation's existing migrator before starting the host, including flattened SQLite and PostgreSQL installations. Back up locale assignments with the content database and locale-aware layout settings with `layouts.json`.

`ContentLocales` stores versioned `ContentLocale` records (`Id`, `EntityType`, `EntityId`, nullable `Locale`). The named `ContentLocale` EF filter uses the common `IContentLocaleContext`, `IContentLocaleStore` and `ContentLocaleScope` contracts. Null assignments make roots neutral and children inherit the effective parent locale. Module edit policies are declared through `ModuleDatabase.ContentLocaleEditPolicies`; ownership-sensitive checks use `IContentLocalePermission`. Raw editing and audit labels include the new entity.

## Scheduled site messages

Core's `Messages` table stores immutable `(Key, Version)` versions with `Name`, `Title`, `Content`, `Level`, nullable `StartsAt` and nullable `EndsAt`. Schedule bounds are UTC ticks, using the existing `DateTimeOffsetConverter`; creation/update times and deletion state come only from the audit log. `AddMessages` migrations exist for SQLite and PostgreSQL and are applied through the existing Core migration chain, also for flattened site databases. Run the application database migrator before starting the updated host. The raw editor discovers all columns automatically and audit labels resolve the message name.

`IMessageQueries.FindLive` selects the latest active version first, then applies the half-open schedule interval. Management queries support search, letters, paging and deleted state; `IMessageCommands` reuses `IVersionedCommands` for create/update, soft delete, restore and permanent deletion.

## Layout configuration persistence

Layouts are configuration, not database entities: `<Backend:DataDirectory>/layouts.json` stores the schema version, configuration revision, default layout, layouts (including soft-deleted ones), and route assignments. No database migration, entity audit label or raw database table is introduced. Back up this file alongside the database. A missing file uses configurable header, content and footer sections. Versionless/schema-0 files are upgraded in memory to schema 1 with header/footer widgets; the file and revision remain unchanged until the next successful save. Schema-1 files preserve intentionally headerless/footerless pages. Unsupported schemas and malformed files produce an explicit error and are never silently overwritten. Writes use a singleton asynchronous gate, optimistic revision comparison and atomic file replacement. Run one writer application per data directory; do not share the file between independent writer processes. Restore and permanent deletion require `RawDatabase.Manage`; ordinary saves retain hidden deleted layouts.

Chat history is stored in the shared database's `ChatMessages` table with a composite `(Key, Version)` primary key and indexes on `AuthorId` and `ReplyToId`. SQLite and PostgreSQL migrations add it to the Core chain, including flattened site databases. Edits insert versions; moderation soft deletes all versions through the audit log. Serializable write transactions protect reply validation and edits. The raw editor discovers the table automatically, and audit labels identify messages by author. History uses descending audit creation time and key as a deterministic cursor; it has no retention cap. The old `<Backend:DataDirectory>/widget-chat.json` file is not read, modified or imported: database chat starts fresh. Apply the database migrator before starting the updated host and include the database in backups.

Module feeds reuse audit-derived creation ordering through Common.Database's `IModuleCreationOrder`, implemented by Core's `ModuleCreationOrder`; they do not introduce timestamp columns. Its `CreatedAtAsync` batch-reads first-version audit creation times and returns null for missing entries.

## Forum persistence and profile pictures

The versioned `ContentRestrictions` table stores numeric per-entity minimum ages, while `ContentAgeOptions` stores the selectable positive ages and their display names. Renaming inserts a new option version without changing assignments; migration assigns the numeric age as the initial name of existing rows. Removing an option leaves existing numeric restrictions unchanged. `ContentAge` and `HiddenContentIds` apply shared query filtering and transitive parent references. Modules declare filterable types and parent property names through `ModuleDatabase.ContentRestrictedEntities` and `ContentRestrictionParentProperties`, and their locale edit policies and ownership checks also authorize age edits. Global `Backend.ContentRestrictions.Enabled` defaults to false and bypasses age filtering without discarding rules.

Profile details add nullable `Users.Birthday` and `Users.HideEmail` (default true); apply the SQLite/PostgreSQL migrations, including flattened installation contexts, before starting the host. Existing accounts keep their email private and have no birthday until explicitly saved. Birthdays are sensitive raw-editor data, not public module profile fields. Account labels are derived from the full UUID, so no additional name-tag column is required.

Scheduled content with equal start times is ordered by audit creation time before the id; the ban list also uses audit creation timestamps rather than UUID order. Neither change adds entity timestamp columns.

`IForumContext` is implemented by the main SQLite/PostgreSQL project contexts, flattening `ForumBoards`, `ForumPosts` and `ForumUserModerations` into the same database as Core. Each table uses the composite `Key`/`Version` primary key, append-only updates, audit-derived timestamps, soft deletion, developer restore/permanent deletion and generic raw editing. The module migrations use `__forumMigrationsHistory` and are applied by the web project migrator after Core. Parent references have no cross-module foreign keys; commands validate active boards and published topics and refuse permanent deletion of referenced boards/topics.

Core adds nullable `Users.ProfilePictureId`, migrated for both providers. Picture metadata uses the existing versioned VFS; content is stored below `Backend:DataDirectory` from `backend.json` as `vfs/Users/<user id>/<file id>`. Uploads are attached to the session user immediately. Replaced/removed images are no longer served by the profile endpoint; existing browser caches may retain previously downloaded bytes.

This document covers the database layer design, Entity Framework Core patterns, repository implementation, and data access strategies.

## Overview

Behind uses **Entity Framework Core 11.0.0-rc.3.26522.148** as the ORM with support for multiple database backends:

- **SQLite 3.53.3** - Default for development (`SQLitePCLRaw.lib.e_sqlite3` 3.53.3)
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
```

EF Migrations live in `Behind.Core.Database.Sqlite/Migrations` (core context, `CoreContextSqlite`) and `Behind.Core.Database.Sqlite/Migrations/Versioning` (versioning context, `VersioningContextSqlite`). The PostgreSQL provider has its own migrations in `Behind.Core.Database.Postgres/Migrations` (`CoreContextPostgres`) and `Behind.Core.Database.Postgres/Migrations/Versioning` (`VersioningContextPostgres`).

### Database Providers

`database.json` selects the provider (`"Provider": "Sqlite"` or `"Provider": "Postgres"`) and the connection string by its `Key`. Every provider project (`Behind.Core.Database.Sqlite`, `Behind.Core.Database.Postgres`) has the same shape:

- a core context and a versioning context that only add provider conventions (`ApplyCoreProviderConventions`, `ApplyVersioningProviderConventions`: UUID v7 ids and the tick based audit timestamps, shared in `Behind.Core.Database`),
- an `ICoreDatabaseMigrator` (`SqliteCoreDatabaseMigrator` also runs the VFS legacy migration, `PostgresCoreDatabaseMigrator` applies the migrations) used by `Behind.Core.Database.Migrator`,
- `RegisterSqlite` / `RegisterPostgres` extension methods for `DatabaseConfiguration` (web application and migrator host) that call the shared `AddAuditedDatabase` / `AddMigrationDatabase` of `Behind.Core.Database` and only register when the configured provider matches.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=behind;Username=behind;Password=<password>"
  },
  "Database": {
    "Provider": "Postgres",
    "Key": "DefaultConnection"
  }
}
```

Dates are stored as UTC ticks (`bigint`) on both providers. The PostgreSQL migrations are a fresh `Initial` migration generated from the model; databases that were created with SQLite are not converted.

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

## Two-factor authentication

Three tables hold the second factor of a user, each a plain entity with a generated `Id`, a `UserId` foreign key to `Users` (cascade delete, so hard deleting a user removes them) and no soft delete (a removed passkey or used recovery code is gone for good, the audit log keeps who did it):

- `UserTotps` (`UserTotp`): `Secret` (Base32, `[Sensible]`), `IsConfirmed` (a pending secret is not a second factor until a code was confirmed) and `LastUsedStep` (replay protection); one row per user (unique `UserId`).
- `UserPasskeys` (`UserPasskey`): `Name`, `CredentialId` (unique, base64url), `PublicKey` (base64url) and `SignCount`. The creation time is read from the audit log, never stored.
- `UserRecoveryCodes` (`UserRecoveryCode`): `Hash` (BCrypt, `[Sensible]`). A code is deleted when it is used.

`ITwoFactorQueries` / `ITwoFactorCommands` (`TwoFactorQueries`, `TwoFactorCommands`) read and change them; the step and the sign count are advanced with conditional updates so a replay loses the race. They are listed in `AuditLabelExtensions` ("Authenticator app of ...", "Passkey of ...", "Recovery code of ...") and are editable in the raw database editor (the secrets are masked). Migration `AddTwoFactor`.

## User settings

The `UserSettings` table (migration `AddUserSettings`) holds simple per-user key/value pairs: `UserId` (foreign key to `Users`, cascade delete), `Key` (max 64) and `Value` (max 256), with the primary key `UserId` + `Key`. It is a shared-type join table like `UserGrants`, not an audited entity, so it is read and written through `IUserSettingQueries` / `IUserSettingCommands` and has no audit label. The only key so far is `locale` (`UserSettingKeys.Locale`).

## Versioned Sites

`Site` is the first versioned entity. Its identity is a `VersionedKey<Guid>` (`Behind.Common`, next to `ActiveVersion` in `Behind.Common.Database`) (the site id and a version number starting at 1) and the version that visitors see is decided by an `ActiveVersion` row, never by the site row itself.

- `Sites` has the composite key (`SiteId`, `Version`). The domain `Site.Id` is ignored by EF and stored in the two shadow columns, like the other entities keep their ids. A save never updates a row: `ISiteCommands.Create` / `SaveVersion` insert version `latest + 1` (a concurrent insert of the same version fails on the key and returns `SiteSaveResult.VersionConflict`).
- `HideTitle` (`bool`, default `false`) is stored per version: when set, the public page renders the content (and carousel) without its title heading.
- `PublishedAt` (`Option<DateTimeOffset>`) is the draft flag: `None` is a draft. It is stored in the shadow column `PublishedAtUtc`. The only update ever made to a `Sites` row is stamping `PublishedAtUtc` when a draft is made live with `ISiteCommands.Activate`.
- `ActiveVersions` (`EntityType`, `EntityKey`, `Version`, key `EntityType` + `EntityKey`) holds the live version, `EntityType` is `Site.EntityType` and `EntityKey` the site id. It is a general table, so it has its own context: `AbstractVersioningContext` (`VersioningContextSqlite`) with its own migrations in `Migrations/Versioning` and its own history table (`__VersioningMigrationsHistory`). `ActiveVersion`, its mapping and `ActiveVersionsAsync` / `SetActiveVersionAsync` live in `Behind.Common.Database`. The migrator applies the core migrations first and the versioning ones second.
- The versioning context is audited like the core one (same `AuditSaveChangesInterceptor`): it maps `AuditLogEntry` onto the existing `AuditLogEntries` table with `ExcludeFromMigrations`, so its migrations never create or change that table. Publishing or activating adds the `ActiveVersions` row or replaces it through `UpdateImmutable`, so the audit log shows an `Updated` entry with the `Version` change.
- The two contexts are saved one after the other (no shared transaction): `SiteCommands` saves the `Sites` row first and the `ActiveVersions` row second, so a failure in between leaves a published version that is not live yet, never a live version that does not exist.
- A slug is taken when another site has it in its live or its newest version (`IsSiteSlugTakenAsync`), which is checked on save and on activation (`SiteSaveResult.SlugTaken`). `ISiteQueries.FindActiveBySlug` resolves the slug of the live versions only, drafts never leak.
- Uploaded site images are files of the VFS (`VfsFile`, type `Sites`, see "Virtual file system" below). They are served by `GET /sites/images/{id}` with an immutable cache header.
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
- Header and content images are VFS files (`POST /news/images` is `SiteImageEndpoints.UploadNews`, stored with the type `News` until it is attached, behind the `News.Write` grant, images are served by `GET /news/images/{id}`).
- Audit labels: `AuditLabelExtensions` names a news by the title of its newest version, a category by its name and a comment as `News comment of <author>`; the audit keys are `<id>:<version>`, so `LabelKey` shows `Title: 2`.
- Migrations: `MakeNewsVersioned` renames `Id` to `Key`, adds `Version` (existing rows become version 1), drops the foreign keys between the news tables and rewrites the audit keys to `<id>:1`; `PublishExistingNews` (versioning context) makes the existing news live.

The `News` grant area (`News.Read`, `Write`, `Delete`, `Manage`) is administration only: `Users` do not get `News.Read`. Reading the news is public, commenting needs a signed in session, deleting a comment needs to be its author or to hold `News.Delete`.

## Articles

`Article` and `ArticleCategory` are versioned entities exactly like `News` and `NewsCategory` (`VersionedKey<Guid>` id, drafts, `ActiveVersions` with `Article.EntityType`, soft delete, hard delete, restore), with these differences: an article has only `Title`, `Summary` and `Content` (plus the shadow `AuthorId` and `CategoryId`), no schedule, no highlight and no comments, and it has its own categories (`ArticleCategory`, `Name`, optional `HeaderImageId`) which are independent from the news categories.

- Search: `ArticleQueryExtensions.Matching` filters on the lower cased title or content (`IArticleQueries.List` searches the live versions, `ListManaged` and `GetLetters` the latest versions).
- Audit labels: `AuditLabelExtensions` names an article by the title of its newest version and an article category by its name.
- Migration: `AddArticles` creates `Articles` and `ArticleCategories`.

The `Article` grant area (`Article.Read`, `Write`, `Delete`, `Manage`) is administration only, like `News`: `Users` do not get `Article.Read`. Reading the articles is public.

## Galleries

`Gallery` and `GalleryItem` are versioned entities (`VersionedKey<Guid>` id, drafts through `ActiveVersions` with `Gallery.EntityType`, soft delete, hard delete, restore) like `Article`.

- `Gallery`: `Title`, `Description` plus the shadow `AuthorId`, `ThumbnailImageId` (optional `VfsFile` key, the uploaded or generated gallery thumbnail) and `ThumbnailGenerated` (the thumbnail is regenerated from the first items on every save).
- `GalleryItem`: `Description` plus the shadow `GalleryId` (the gallery key, no foreign key because the keys are composite), `ImageId` and `ThumbnailId` (both `VfsFile` keys of the `Galleries` type) and `Position`. None of these keys has a database foreign key because `VfsFile` has a composite key; `GalleryCommands` checks that every referenced image exists with the `Galleries` type. The version of an item is the version of the gallery it belongs to: every save of a gallery inserts the new gallery row and a fresh row per item, so a version is complete on its own and activating an older version shows exactly its images. Items are not soft deleted on their own; `Gallery` is registered for soft delete and hard deleting a gallery removes all its items.
- Item descriptions are optional: they may be empty, in drafts and in published versions.
- Thumbnails: `Behind.Common` (`ImageThumbnailExtensions`, SkiaSharp) decodes the image, applies the EXIF orientation, scales it down and encodes WebP; the original image and its thumbnail are stored as `GalleryImage` rows.
- Gallery images are files of the `Galleries` VFS type (own upload endpoints and own size limit, but the shared `VfsFile` store). They are not soft deleted; hard deleting a gallery deletes the files that only this gallery used (its item images, item thumbnails and gallery thumbnails, through `IVfsCommands.Delete`, which also removes the content) and keeps the ones another gallery still references. Saving a gallery attaches its images to it (`IVfsCommands.Attach`, see below). Uploads that were never saved in a gallery stay in the VFS.
- Audit labels: `AuditLabelExtensions` names a gallery by the title of its newest version and an item by its description (shortened to 80 characters, `Image` when it has none) and a VFS file (site or gallery image) by its file name.
- Migrations: `AddGalleries` creates `Galleries` and `GalleryItems`, `GalleriesOwnImages` creates `GalleryImages` and drops the foreign keys to `SiteImages`; both image tables are replaced by the VFS, see below.

The `Gallery` grant area (`Gallery.Read`, `Write`, `Delete`, `Manage`) is administration only, like `Article`. The built-in main menu has a `Galleries` tab (`#/galleries`).

## Site sections

`SiteSection` is a versioned entity (`VersionedKey<Guid>` id) that turns a site into a section based page, for example a landing page with pictures next to the texts. The sections belong to one version of a site and are shown below its content.

- `SiteSection` (`SiteSections`): `Theme` (`SiteSectionTheme`: `Normal` or `Accented`, stored as text), `Title` (up to 200 characters, may be empty), `Content` (sanitized rich text, up to 100000 characters, may be empty), `ImageAlt` (up to 300 characters) and `ImagePosition` (`SiteSectionImagePosition`: `Left` or `Right`, stored as text) plus the shadow columns `SiteId`, `ImageId` (nullable, a `VfsFile` key of the `Sites` type, no foreign key) and `Position`. A section needs a title, a text or a picture.
- The key of a section is a fresh uuid and its version is the version of the site it belongs to. Like `GalleryItem`, every `ISiteCommands.Create` / `SaveVersion` inserts a fresh row per section together with the new site version, so every version is complete on its own, older versions keep their sections and a version saved without sections has none. A site has at most `Site.MaxSections` (30) sections.
- Sections are not soft deleted on their own: they follow the site. `Delete` and `Restore` of a site leave them in place (they are only read through a site version that is visible), `HardDelete` removes the sections of the deleted versions with the site.
- `SaveVersion` and `Create` check that every `ImageId` is an existing file of the `Sites` VFS kind and answer `SiteSaveResult.UnknownImage` (nothing is saved) otherwise. After the save the section images and the images embedded in the section content are attached to the site (`IVfsCommands.Attach`, content under `vfs/Sites/<site uuid>/<image uuid>`), exactly like the images of the site content. Section images are uploaded with the site image endpoint (`POST /sites/images`) and served from `/sites/images/<id>`.
- Reading: `ISiteQueries.FindSections(VersionedKey<Guid>)` returns the sections of one site version in order. The public page (`GET /sites/slug/{slug}`, `GET /sites/index`) and the editor (`GET /sites/{id}/versions/{version}`) both include them.
- Audit labels: a section is named by its title (`Section` when it has none), `SiteSection` entity name.
- Raw database editor: `SiteSections` has a composite key and no sensitive column, so it is editable without any extra registration.
- Migration: `AddSiteSections` creates `SiteSections`.

## Site carousels

`Carousel` and `CarouselSlide` are versioned entities (`VersionedKey<Guid>` id, a save inserts the next version and never updates a row, soft delete, hard delete, restore) that give a site an optional image slider.

- `Carousel` (`Carousels`): `IntervalSeconds` (inside the configured range). The key of a carousel is the key of its site (no foreign key because the keys are composite), so a site has at most one carousel and every save of it adds a version. A save makes the new version live (`ActiveVersions` with `Carousel.EntityType`), there are no drafts.
- `CarouselSlide` (`CarouselSlides`): `Caption` (up to 1000 characters, the configured limit is lower or equal) and `LinkUrl` (both may be empty) plus the shadow `CarouselId` (the site key), `ImageId` (a `VfsFile` key of the `Carousels` type, no foreign key) and `Position`. Like `GalleryItem`, every save inserts the new carousel row and a fresh row per slide, so a version is complete on its own. Slides are not soft deleted on their own.
- `CarouselCommands.Save` needs a site that is not deleted and checks that every image exists with the `Carousels` VFS type. The next version is computed over all rows including the deleted ones, so a carousel can be created again after it was removed.
- Deletion: `Delete` soft deletes every version, `Restore` brings the deleted versions back and `HardDelete` (only deleted rows) removes them with their slides and the images no other slide uses, and clears the active version once no version is left. `SiteCommands` calls them when a site is deleted, restored or permanently deleted, so the carousel follows its site.
- Public reading: `ICarouselQueries.FindLive` only answers for a site that has a live version, so the carousel of a draft or deleted site is never served. The management read (`FindManaged`) returns the latest carousel in the requested deletion state.
- Images: the `Carousels` VFS type has its own kind (`VfsTypes.CarouselImages`), its own URL `/carousels/images/<id>` and its own upload endpoint. A save attaches the images to the site (`IVfsCommands.Attach`, content under `vfs/Carousels/<site uuid>/<image uuid>`).
- Optional per site: a site only has a carousel once one is saved, a site without one is served exactly as before. The limits (slides, interval range, caption length, image size) are configuration (`Frontend:Carousels`, `CarouselsConfiguration`) and checked by the endpoint, see the web API conventions; the database only stores the values.
- Audit labels: the carousel key is the site key, so the audit log shows the title of the site (`Carousel` entity name); a slide is named by its caption (`Image` when it has none).
- Raw database editor: both tables are editable like every other table, no registration is needed.
- Migration: `AddCarousels` creates `Carousels` and `CarouselSlides`.

## Downloads

`DownloadFolder` and `Download` are versioned entities (`VersionedKey<Guid>` id, a save inserts the next version and never updates a row, no drafts, soft delete, hard delete, restore) like `Menu`.

- `DownloadFolder` (`DownloadFolders`): `Slug` (a slug, unique among the active siblings ignoring case, used in the path `#/downloads/<slug>/<slug>`) and `DisplayName` plus the shadow `ParentId` (the key of the parent folder, `null` at the top, no foreign key because the keys are composite). The parent of a folder does not change.
- `Download` (`Downloads`): `DisplayName` (defaults to the file name) plus the shadow `FolderId`, `FileId` (the key of the `VfsFile`, no foreign key) and `UploadedById` (foreign key to the human user). The size, the file name and the content type come from the `VfsFile`, the uploaded at time is the creation entry of the audit log (`AuditTimesAsync<Download>`), never a column.
- The file is a file of the `Downloads` VFS type. `DownloadCommands.Add` stores the file (`IVfsCommands.Add`), inserts the download and then attaches the file to it (`IVfsCommands.Attach`, the content moves to `vfs/Downloads/<download uuid>/<file uuid>`); a failed save removes the file again.
- Deletion: deleting is soft. A deleted folder hides everything below it because the folder tree is built from the active folders that are reachable from the top (`DownloadQueryExtensions.Reachable`), so the subfolders and files are not touched and come back with the folder. Restoring a folder needs an active parent and a free slug, restoring a download needs an active folder. Hard deleting a deleted folder removes its subfolders and all their downloads and deletes the VFS files (`IVfsCommands.Delete` also removes the content); hard deleting a download deletes its file.
- Audit labels: `AuditLabelExtensions` names a folder and a download by the display name of the newest version.
- Migration: `AddDownloads` creates `DownloadFolders` and `Downloads`.

The `Download` grant area (`Download.Read`, `Write`, `Delete`, `Manage`) is administration only, like `Gallery`: `Read` opens the Downloads tool of the administration page, `Write` creates folders and uploads and renames files, `Delete` deletes them. Browsing and downloading the files is public. The built-in main menu has a `Downloads` tab (`#/downloads`).

## Virtual file system

Everything that is uploaded is a file of one virtual file system (VFS) instead of a blob column of its own table:

- `VfsFile` (`VfsFiles` table) holds the metadata: a `VersionedKey<Guid>` id (rows are never updated, a change inserts the next version), `Type` (the kind of item the file belongs to: `Sites`, `News`, `Articles`, `NewsCategories`, `ArticleCategories`, `Galleries` or `Downloads`, see `VfsTypes`), `FileName`, the sniffed `ContentType`, `Length` and the optional `ParentId`, the key of the item the file belongs to (no foreign key). It has no time columns: the creation time is the audit entry, and the audit label of a file is its file name.
- The content is not in the database. `IContentStore` (`Behind.Common.Storage`, implemented by `DirectoryContentStore`) writes it atomically below `<DataDirectory>/vfs`, every key is checked to stay inside that root. The layout is `vfs/<Type>/<parent uuid>/<file uuid>` for a file with a parent and `vfs/<Type>/<file uuid>` for a file without one (`VfsFile.StorageKey`, for example `vfs/Articles/<article uuid>/<file uuid>` or `vfs/Sites/<file uuid>`). The path does not contain the version.
- `DataDirectory` is read from `backend.json` (`BackendConfiguration`, section `Backend`, for example `{ "Backend": { "DataDirectory": "/app/data" } }`). The file is required and the application fails at startup when `Backend:DataDirectory` is missing (`AddVfsContentStore`). The Web application and the migrator must point to the same directory, and the directory needs to be backed up together with the database.
- `IVfsCommands.Add` writes the content first and then the row (a failed save removes the content again) and the file has no parent yet. `IVfsCommands.Attach(type, parentId, keys)` is called when the parent becomes known: it moves the content into the `<type>/<parent uuid>` folder and inserts the next version of the file with the owner `Type` and the `ParentId` (files that have a parent already are left alone, so the first parent wins; a failed save moves the content back). `GalleryCommands` attaches the item images, item thumbnails and the thumbnail of a gallery to the gallery key after every save, and the site image users attach theirs after every save (`IVfsCommands.AttachSiteImagesOf` finds the `/sites/images/<id>` URLs with `ReferencedKeys`): `SiteCommands` (content), `NewsCommands` and `ArticleCommands` (summary and content) attach to the site, news or article key, and the news and article category commands attach their header image to the category key. A site image therefore ends up in `vfs/<Sites|News|Articles|NewsCategories|ArticleCategories>/<parent uuid>/<file uuid>`; an upload that is not used yet stays in the folder of the endpoint it was uploaded to (`/sites/images`: `Sites`, `/news/images`: `News`, `/articles/images`: `Articles`). Only files of the same kind are attached: the site image types (`VfsTypes.SameKindAs`) never mix with the gallery images. `IVfsCommands.Delete` removes all the versions and the content. `IVfsQueries.Open(types, key)` returns the newest version with the bytes and only finds files of the given types. Every kind of content serves its images under its own URL: `GET /sites/images/{id}` (`Sites`), `GET /news/images/{id}` (`News`, `NewsCategories`), `GET /articles/images/{id}` (`Articles`, `ArticleCategories`) and `GET /galleries/images/{id}` (`Galleries`), and the upload endpoints answer with the URL of their content (`VfsTypes.ImageUrlOf`). The news and article content refer to their images with their own URL, `ReferencedImageUrlsOf` also accepts the old `/sites/images/<id>` URLs that are stored in older content, and `GET /sites/images/{id}` still serves every site image type so those older URLs keep working, so a gallery image is never served as a site image. A row without its content is answered as not found.
- Related tables keep plain `Guid` references without foreign keys (`NewsCategories.HeaderImageId`, `ArticleCategories.HeaderImageId`, `GalleryItems.ImageId`, `GalleryItems.ThumbnailId`, `Galleries.ThumbnailImageId`) because the key is composite; the commands check that the file exists in the right directory.
- Migrations: `AddVfsFiles` creates `VfsFiles`, copies the metadata of `SiteImages` (the type and the parent are the site, news, article or category that uses the image, `Sites` without a parent otherwise) and `GalleryImages` (type `Galleries`, the parent is the gallery whose items or thumbnail use the image, when there is one; same keys, version 1) and drops the foreign keys from the category tables; `DropLegacyImages` drops the two old tables. The blobs cannot be moved by SQL, so the migrator runs `MigrateWithVfsAsync`: when `DropLegacyImages` is pending it migrates to `AddVfsFiles`, writes the legacy blobs into the content store at their `StorageKey` and then applies the remaining migrations. The foreign keys must not be dropped in the same migration that drops the old table, because SQLite would null the header images when the table is dropped.

## Menus

`Menu` and `MenuItem` are versioned entities (`VersionedKey<Guid>` id, a save inserts the next version and never updates a row, soft delete, hard delete, restore) without drafts: the latest version is the current one, like the categories.

- `Menu`: `Name` (a slug, unique among the active menus ignoring case, used to request the menu) and `Title`. The built-in `main` menu (`BuiltInMenus.Main`, fixed ids) cannot be renamed, deleted or permanently deleted.
- `MenuItem`: `Title`, `Url`, `Target` (`CurrentPage`, `NewTab`, `NewWindow`, stored as text), `Kind` (`Link`, `User`) and `Position`, plus the shadow `MenuId` and `ParentId` (keys, not versions, no foreign keys because the keys are composite). A `ParentId` makes the item a sub item: the parent must be an active top level `Link` of the same menu, sub items are one level deep. `User` items are the entries of the user menu, only allowed at the top level of the main menu (`MenuItemKind` is checked in `MenuItemCommands`).
- Visibility: the shared join entity `MenuItemRoles` (`MenuItemKey`, `MenuItemVersion`, `RoleId`, cascade delete with the item version, no database foreign key to the roles because `Role` is mapped table per concrete type) stores the roles of every item version. No row means every role and visitors who are not signed in. `MenuVisibilityExtensions.IsVisibleTo` and `MenuTreeExtensions.ToVisibleTree` decide what a viewer sees; a sub item is hidden together with its parent. Deleting a role softly makes its items invisible to everybody until the role is restored.
- Deletion: deleting a menu or an item is soft and hides the item together with its sub items; hard deleting a menu removes all of its items, hard deleting an item removes its sub items.
- Seeding: `EnsureBuiltInMenusAsync` (called by the migrator after the built-in roles) creates the main menu with the tabs (`Index`, `News`, `Articles`, `Galleries`, `Downloads`, `Wall of shame`) and the user menu items (`Profile`, `Session`, `Sessions`, and `Administration` for the `Administrators` and `Developers` roles) once; items that were deleted or changed by an administrator are not recreated.
- Audit labels: `AuditLabelExtensions` names a menu by the title of its newest version and a menu item by its title, so the audit keys of `MenuItemRoles` show the item and the role names.
- Migration: `AddMenus` creates `Menus`, `MenuItems` and `MenuItemRoles`.

The `Menu` grant area (`Menu.Read`, `Write`, `Delete`, `Manage`) is administration only: `Users` do not get `Menu.Read`. Resolving a menu for the page is public.

## Route Aliases

`RouteAlias` is a versioned entity (`VersionedKey<Guid>` id, a save inserts the next version and never updates a row, soft delete, hard delete, restore) like `Menu`. It maps a short route to a page of the application, for example `manuals` to `#/downloads/products/manuals`.

- Columns: `Alias` (a slug of up to 100 characters, unique among the active route aliases, indexed) and `Target` (up to 2000 characters, validated by `RouteTargetExtensions.IsValidRouteTarget`: it starts with `#/` and holds no whitespace, control characters or backslashes).
- Queries: `IRouteAliasQueries.FindTarget` answers the target of the newest version of an active alias; the management list is paged with search, letters and the deleted state.
- Audit labels: `AuditLabelExtensions` names a route alias by the alias of its newest version, also when it is deleted.
- Migration: `AddRouteAliases` creates `RouteAliases`.

The `RouteAlias` grant area (`RouteAlias.Read`, `Write`, `Delete`, `Manage`) is administration only, like `Menu`. Resolving an alias is public. The table is editable in the raw database editor like every other table.

## Access Settings and User Approvals

`AccessSettings` is a versioned singleton (`VersionedKey<Guid>` id with the fixed key `AccessSettings.SingletonKey`, a save inserts the next version and never updates a row, soft delete, hard delete, restore). It holds the restriction page (`RestrictionEnabled`, `RestrictedToRole`, `RestrictionTitle`, `RestrictionMessage`) and `ModeratedSignUp`. Without active rows `AccessSettings.Open` applies (everything off).

- Table `AccessSettings`: `Key`, `Version`, `RestrictionEnabled`, `RestrictedToRole` (up to 100 characters), `RestrictionTitle` (up to 200 characters, empty means the default heading), `RestrictionMessage` (sanitized rich text, up to 20000 characters), `ModeratedSignUp`.
- Queries: `IAccessSettingsQueries.Current` answers the latest active version (or `Open`), `FindDeleted` the latest deleted version while no active one exists.
- Audit labels: `AuditLabelExtensions` names the entity `Access settings`.
- Images: the message may hold images. They are files of the `AccessSettings` VFS type with their own kind (`VfsTypes.AccessImages`), their own URL `/access/images/<id>` and their own upload endpoint. A save attaches the images referenced by the message to the singleton key (`IVfsCommands.Attach`), so the content moves to `vfs/AccessSettings/<singleton key>/<file uuid>`.
- Table `PendingUserApprovals` (`UserId`, a foreign key to the user with cascade delete): a row means the user waits for approval, no row means the user may sign in. Existing users therefore stay allowed. `IUserApprovalCommands.RequireApproval` adds the row (sign-up while moderation is on), `Approve` removes it; `IUserApprovalQueries` answers `IsPending` and the paged pending list (soft deleted users are left out). `IdentityStore` fills `ApplicationUser.PendingApproval` from it.
- Migrations: `AddAccessSettingsAndUserApprovals` and `AddAccessRestrictionTitle` (SQLite and PostgreSQL).

The `Access` grant area (`Access.Read`, `Write`, `Delete`, `Manage`) is administration only. `Access.Manage` also exempts a user from the restriction page (`AccessSettingsExtensions.IsRestrictedFor`). Both tables are editable in the raw database editor like every other table.

## Raw Database Editors

Developers (`RawDatabase.Manage`) can edit the rows of every table as they are stored (`#/manage/raw-database`, see `WEBAPI_CONVENTIONS.md`). The editor is generic and driven by the EF Core model, so a new entity needs no extra code to appear in it:

- `DbContext.RawTables` (`Behind.Common.Database.Raw.RawTableExtensions`) turns every table of the relational model with a primary key into a `RawTable` of `RawColumn`s (name, store type, `RawValueKind`, nullability, key, `IsSensitive` from `[Sensible]`). `RawTable.EntityName` is the audit entity name (`IReadOnlyEntityType.AuditEntityName`, shared with the EF audit).
- `RawDatabaseExtensions` reads, counts, inserts, updates and deletes rows with parameterised SQL. Table and column names are only taken from the model, values are parsed by the kind of their column (`RawColumn.Parse`) and read values are normalised (`Normalize`: Guid text, Base64 binary, booleans) and masked (`Present`, `RawTable.Mask`).
- `RawDatabaseCommands` (Core) writes inside a transaction and records the audit entry itself (`DbContextAuditExtensions.AddAuditEntry`), because raw SQL bypasses the change tracker: `Created`, `Updated` (differences of the row before and after) and `HardDeleted`. `RawDatabasePolicyExtensions` lists the read-only entities (`AuditLogEntry`).
- Soft delete is not applied: raw reads see every row and a raw delete removes the row for good.
- `RawDatabaseCatalogTests` fails when an entity type of the model is not covered by the editor, which is how a new entity is checked (see the `add-raw-database-editor` skill).

## Modules and the flattened database

Behind can host more modules next to the required Core module. A module never references another module (nor Core): it only references the `Behind.Common*` projects and describes itself with a `ModuleDescriptor` (`Behind.Common.Modules`), including its `ModuleDatabase`:

- `ConfigurationAssembly`: the assembly holding the module `IEntityTypeConfiguration<T>` classes.
- `SoftDeletedEntities`: the root entities that get the soft delete query filter.
- `ReadOnlyEntities`: the entity names the raw database editor must not write.

The database is flattened in the project: one database and one DbContext per provider. The project context derives from `AbstractCoreContext` (the Core context, which is required and always loaded), implements the context interfaces of every module (`INotesContext` with its `DbSet`s) and receives the `ModuleCatalog` in its constructor (`AbstractCoreContext(options, modules)`), so `OnModelCreating` applies the entity configurations and the soft delete of every module. A module context is only ever exposed as an interface: a module registers `services.AddModuleContext<INotesContext>()`, which resolves the project context through `IUnitOfWork` and fails fast when the project context does not implement the interface.

Registration (the Core defaults stay `RegisterSqlite(builder)`/`RegisterPostgres(builder)`):

```csharp
database.RegisterSqlite<WebContextSqlite>(builder);
database.RegisterPostgres<WebContextPostgres>(builder);
```

registers the project context as `AbstractCoreContext`/`IUnitOfWork`, keeps the Core migrator working through the plain `CoreContextSqlite`/`CoreContextPostgres` and registers the `AccessCatalog` (Core plus module grants).

### Migrations of modules

The Core keeps its own migration chain (`CoreContextSqlite`, history table `__EFMigrationsHistory`). Every module owns its migration chain too, with its own history table (`ModuleDescriptor.MigrationsHistoryTable`, `__<key>MigrationsHistory`), so the chains are independent and a module can be added to or removed from a project without touching the Core migrations:

1. The module provider project (`Behind.<Module>.Database.Sqlite`/`.Postgres`) holds a design-time only context (`<Module>ContextSqlite : DbContext`) that applies only the module model (`modelBuilder.ApplyModuleConfigurations(...)`), the migrations and a design-time factory that uses `module.MigrationsHistoryTable`.
2. The project migrator project registers it: `database.RegisterSqliteModule<BlogContextSqlite>(builder, BlogModule.Descriptor)` (`RegisterPostgresModule` for PostgreSQL). That registers an `IModuleMigrator` (`ContextModuleMigrator<TContext>`) per module.
3. `MigratorService` applies the Core migrations, then every `IModuleMigrator`, then the versioning migrations and finally the seeds: built-in users, built-in roles (`EnsureBuiltInRolesAsync(accessCatalog)` adds the grants of the modules to the built-in roles and keeps the edits of the administrators), built-in menus (`EnsureBuiltInMenusAsync(moduleCatalog)` adds the default menu items of the modules) and every `IModuleSeed` of the modules for their own default data.

Module tables never have foreign keys to Core tables; they refer to users and other Core records by id only.

### Audit labels and the raw database editor of modules

- A module registers an `IAuditLabelSource` (`LabelsAsync(ids)` returning `AuditLabel(id, label)`) so the audit log shows human readable names for its entities; `AuditLogEntryQueries` merges the labels of every source with the Core ones.
- Every table of the flattened model is editable in the raw database editor automatically. Mark sensitive properties with `[Sensible]` and list read-only tables in `ModuleDatabase.ReadOnlyEntities`.

### The Quotes example module

`Quote` is stored in the `Quotes` table (`Key`, `Version`, `Text`, `Author`; composite primary key `Key` + `Version`, index on `Author`). A save inserts the next version and never updates a row; the created and updated times come from the audit log, so the table has no timestamp columns, and it has no foreign keys. `Quote` is listed in `ModuleDatabase.SoftDeletedEntities`, so deleting soft deletes (all versions), developers list deleted quotes, restore them and delete them permanently.

The module commands and queries use the module context interface `IQuotesContext`, which extends `IModuleContext` (`Behind.Common.Database/Modules`): `IModuleContext` is an `ISoftDeleteContext` that hands out the underlying `DbContext` (`Context`) and filters any query by `DeletionState` (`InDeletionState`). `AbstractCoreContext` implements it, so the project context `WebContextSqlite`/`WebContextPostgres` only adds `DbSet<Quote> Quotes` to satisfy `IQuotesContext`, and the module never references Core. The migration history table of the module is `__quotesMigrationsHistory`; the migrations live in `Behind.Quotes.Database.Sqlite/Migrations` and `Behind.Quotes.Database.Postgres/Migrations` and are created from `Behind/` with:

```bash
dotnet ef migrations add "<Name>" --project src/Quotes/Behind.Quotes.Database.Sqlite --startup-project src/Web/Behind.Web.Database.Migrator --context QuotesContextSqlite --output-dir Migrations
dotnet ef migrations add "<Name>" --project src/Quotes/Behind.Quotes.Database.Postgres --startup-project src/Web/Behind.Web.Database.Migrator --context QuotesContextPostgres --output-dir Migrations
```

`QuotesAuditLabelSource` shows the text of a quote (latest version, deleted ones included) in the audit log, `QuotesSeed` adds one welcome quote once, and the `Quotes` table is editable in the raw database editor like every other table.

### The Sport module

The Sport module (`src/Sport`) stores eight versioned entities in its own tables, all with the composite primary key `Key` + `Version`, no timestamp columns (the audit log holds them) and no foreign keys (the references are plain `Guid` columns checked by the commands): `SportDisciplines` (`Name`, `Slug`; Soccer today, any other sport later), `SportPositions`, `SportStatTypes` (`Scope` is `Player` or `Team`, `HigherIsBetter` orders the leaderboards), `SportSeasons`, `SportTeams` (optional `LogoId`), `SportPlayers` (age is calculated from the optional `BirthDate`), `SportPlayerSeasons` (the membership of a player in a season: team, shirt number and the optional picture of that season) and `SportStats` (one value of a stat type for a player or a team in a season). Every entity is listed in `ModuleDatabase.SoftDeletedEntities`; a hard delete is refused while another row still references the entity. All references must belong to the same discipline, which is what makes the module usable for other sports.

`ISportContext` extends `IModuleContext` and exposes the eight `DbSet`s; `WebContextSqlite`/`WebContextPostgres` implement it next to `IQuotesContext`. The migrations (`CreateSport`, history table `__sportMigrationsHistory`) are created like the Quotes ones with `--project src/Sport/Behind.Sport.Database.Sqlite|Postgres --startup-project src/Web/Behind.Web.Database.Migrator --context SportContextSqlite|SportContextPostgres`.

Team logos and player pictures use the VFS through `IModuleFiles` (`Behind.Common.Database/Modules`, implemented by `ModuleFiles` in Core): the types are `SportTeams` and `SportPlayers`, the content is stored as `vfs/SportTeams/<team uuid>/<file uuid>` and `vfs/SportPlayers/<player uuid>/<file uuid>` and the parent is set as soon as the team or the season membership is saved. `SportAuditLabelSource` names every entity in the audit log and `SportSeed` adds Soccer with its positions and stat types once.

## Migrations

### Creating Migrations

```bash
cd Behind

# Create a new migration (core context)
dotnet ef migrations add "AddUserTable" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite \
    --output-dir Migrations

# Create a new migration (versioning context)
dotnet ef migrations add "AddSomething" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context VersioningContextSqlite \
    --output-dir Migrations/Versioning

# Remove last migration
dotnet ef migrations remove \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite
```

The migrations live in `Behind.Core.Database.Sqlite`; apply them by running `Behind.Core.Database.Migrator`.

For PostgreSQL use the same commands with `--project src/Core/Behind.Core.Database.Postgres` and `--context CoreContextPostgres` (output `Migrations`) or `--context VersioningContextPostgres` (output `Migrations/Versioning`). The contexts have design time factories, so the startup project only has to reference `Microsoft.EntityFrameworkCore.Design`. Keep both providers in sync: a model change needs a migration in each provider project (`PostgresMigrations_MatchTheModelSnapshots` fails otherwise).

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
