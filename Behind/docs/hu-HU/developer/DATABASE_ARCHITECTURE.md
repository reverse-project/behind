# Adatbázis-architektúra

Ez a dokumentum az adatbázisréteg felépítését, az Entity Framework Core mintáit, a repository megvalósítását és az adatelérési stratégiákat mutatja be.

## Áttekintés

A Behind az **Entity Framework Core 10.0.10** keretrendszert használja ORM-ként, több adatbázis-háttér támogatásával:

- **SQLite 3.53.3** - Alapértelmezett a fejlesztéshez (SQLitePCLRaw 2.1.12)
- **PostgreSQL** - Aspire-vezénylésen keresztül, haladó telepítésekhez
- **Egyéb adatbázisok** - EF Core szolgáltatókon keresztül potenciálisan támogatottak

## DbContext-architektúra

### DbContext felépítése

A Behind.Core.Database projekt tartalmazza a DbContextet és az entitáskonfigurációkat:

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

Az EF-migrációk a `Behind.Core.Database.Sqlite/Migrations` (core kontextus, `CoreContextSqlite`) és a `Behind.Core.Database.Sqlite/Migrations/Versioning` (verziózási kontextus, `VersioningContextSqlite`) mappában találhatók. A PostgreSQL szolgáltatónak saját migrációi vannak a `Behind.Core.Database.Postgres/Migrations` (`CoreContextPostgres`) és a `Behind.Core.Database.Postgres/Migrations/Versioning` (`VersioningContextPostgres`) mappában.

### Adatbázis-szolgáltatók

A `database.json` választja ki a szolgáltatót (`"Provider": "Sqlite"` vagy `"Provider": "Postgres"`), a kapcsolati karakterláncot pedig a `Key` alapján. Minden szolgáltatóprojekt (`Behind.Core.Database.Sqlite`, `Behind.Core.Database.Postgres`) azonos felépítésű:

- egy core kontextus és egy verziózási kontextus, amelyek csak a szolgáltatói konvenciókat adják hozzá (`ApplyCoreProviderConventions`, `ApplyVersioningProviderConventions`: UUID v7 azonosítók és az ütemszámláláson (tick) alapuló audit időbélyegek, amelyek közösek a `Behind.Core.Database` projektben),
- egy `ICoreDatabaseMigrator` (a `SqliteCoreDatabaseMigrator` a VFS régi migrációját is lefuttatja, a `PostgresCoreDatabaseMigrator` alkalmazza a migrációkat), amelyet a `Behind.Core.Database.Migrator` használ,
- `RegisterSqlite` / `RegisterPostgres` extension metódusok a `DatabaseConfiguration` számára (webalkalmazás és migrátor host), amelyek a `Behind.Core.Database` közös `AddAuditedDatabase` / `AddMigrationDatabase` metódusát hívják, és csak akkor regisztrálnak, ha a beállított szolgáltató egyezik.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=behind;Username=behind;******"
  },
  "Database": {
    "Provider": "Postgres",
    "Key": "DefaultConnection"
  }
}
```

A dátumok mindkét szolgáltatón UTC tickekként (`bigint`) tárolódnak. A PostgreSQL migrációk a modellből generált, új `Initial` migrációk; az SQLite-tal létrehozott adatbázisok nem konvertálódnak.

### DbContext megvalósítása

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // Aggregátumok DbSetként
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Grant> Grants { get; set; }
    public DbSet<Session> Sessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Konfigurációk alkalmazása
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new GrantConfiguration());
        modelBuilder.ApplyConfiguration(new SessionConfiguration());
    }

    // Soft delete támogatás
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

## Entitáskonfiguráció

### Entitástípusok leképezése

A DbContext tisztán tartásához használja a **Fluent API**-t az entitáskonfigurációkhoz:

```csharp
namespace Behind.Core.Database.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasValueGenerator<GuidValueGenerator>();

        // Diszkriminátor az öröklési hierarchiához
        builder.HasDiscriminator<string>("UserType")
            .HasValue<HumanUser>("HumanUser")
            .HasValue<SystemUser>("SystemUser")
            .HasValue<GuestUser>("GuestUser")
            .HasValue<BannedUser>("BannedUser");

        // Az emberi felhasználóra jellemző
        builder.OwnsMany(u => (u as HumanUser)!.Emails, navigationBuilder =>
        {
            navigationBuilder.ToJson();  // Tárolás JSON oszlopként
            navigationBuilder.WithOwner();
            navigationBuilder.HasKey("Id");
            navigationBuilder.Property(e => e.Value)
                .HasMaxLength(255)
                .IsRequired();
        });

        // Kapcsolatok
        builder.HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity("UserRole");

        // Párhuzamosságkezelési token
        builder.Property(u => u.ConcurrencyToken)
            .IsConcurrencyToken();

        // Index a gyakori lekérdezésekhez
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.CreatedAt);
    }
}
```

### Értékobjektumok leképezése

```csharp
// JSON-ként tárolt Email értékobjektum
public class EmailConfiguration : IEntityTypeConfiguration<Email>
{
    public void Configure(EntityTypeBuilder<Email> builder)
    {
        builder.Property(e => e.Value)
            .HasConversion(v => v.Value, v => new Email(v))
            .HasMaxLength(255);
    }
}

// Vagy használjon owned entitásokat
builder.OwnsOne(u => u.Email, navigationBuilder =>
{
    navigationBuilder.Property(e => e.Value)
        .HasColumnName("Email")
        .HasMaxLength(255)
        .IsRequired();
});
```

### Öröklési stratégiák

A Behind a User aggregátumhoz **Table-Per-Hierarchy (TPH)** stratégiát használ:

```csharp
// A User aggregátumban
public abstract record User(Option<IEnumerable<Role>> Roles)
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record HumanUser : User { }
public sealed record SystemUser : User { }
public sealed record GuestUser : User { }
public sealed record BannedUser : User { }

// Egyetlen Users tábla UserType diszkriminátor oszloppal
// Előnyök:
// - Egyszerű lekérdezések
// - Könnyű szűrés
// - Ismert, korlátozott hierarchiához megfelelő
//
// Hátrányok:
// - Null értékű oszlopok az egyes típusokhoz
// - Az egyetlen tábla nagyra nőhet
```

**Mikor melyiket használja: TPH, TPT vagy TPC**:
- **TPH** - Kevés típus, hasonló lekérdezések ✓ A Behindban ezt használjuk
- **TPT** - Sok speciális típus, specifikus lekérdezések
- **TPC** - Nagyon eltérő típusok, ritkán együtt lekérdezve

## Repository megvalósítása

### Általános repository felület

A `Behind.Common.Database` projektben definiálva:

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

### Repository megvalósítási minta

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

        // A késleltetetten betöltött include-ok alkalmazása
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

    // Doménspecifikus lekérdezések
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

### Immutable entitás repository

Csak olvasható adateléréshez:

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

**Használat**:
```csharp
var repo = dbSet.ToImmutableEntityRepository(dbContext, "Roles", "Permissions");
var user = await repo.TryFindAsync(userId);
var allUsers = await repo.FindAsync(new Filter<User>(u => u.Active));
```

## Unit of Work minta

### IUnitOfWork felület

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

### Megvalósítás

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

### A Unit of Work használata

```csharp
public class UserRegistrationService
{
    private readonly IUnitOfWork unitOfWork;

    public async Task<Exceptional<User>> RegisterUserAsync(RegisterDto dto)
    {
        try
        {
            // Kapcsolódó adatok betöltése
            var defaultRole = await unitOfWork.Roles
                .TryFindAsync(Guid.Parse(DomainConstants.DefaultRoleId));

            // Aggregátum létrehozása
            var user = new HumanUser(
                displayName: dto.DisplayName,
                emails: new[] { new Email(dto.Email) },
                roles: defaultRole.AsEnumerable()
            );

            // Mentés a unit of work-ön keresztül
            await unitOfWork.Users.AddAsync(user);
            
            // Auditnapló létrehozása
            var auditLog = new AuditLogEntry(
                EntityName: "User",
                Operation: "Created",
                EntityId: user.Id,
                Changes: "New user registered"
            );
            await unitOfWork.AuditLogs.AddAsync(auditLog);

            // Az összes módosítás atomi véglegesítése
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

## Lekérdezési minták

### Szűrő/Specification minta

```csharp
// Az általános szűrő a lekérdezési logikát foglalja magába
public record Filter<T>(Expression<Func<T, bool>> Predicate)
{
    public IQueryable<T> Apply(IQueryable<T> queryable) => queryable.Where(Predicate);
}

// Doménspecifikus specifikációk
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

// Összeállítás
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

### Lapozás

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

// Használat
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

## Automatikus auditnaplózás

A core kontextus minden `SaveChangesAsync` hívása ugyanabban a tranzakcióban `AuditLogEntry` sorokat ír minden nem audit entitáshoz az `AuditSaveChangesInterceptor` (Behind.Core.Database/Auditing) segítségével:

- **Created / Deleted** a hozzáadott és eltávolított entitásokhoz, **Updated** a módosítottakhoz a megváltozott tulajdonságokkal (a régi és az új értékeket az adatbázisból olvassa). A tényleges változást nem tartalmazó módosításokat nem naplózza.
- Az `EntityName` a gyökér entitástípus (`User`, `Role`, `Email`, `ValidSession`, ...), az `EntityKey` pedig az elsődleges kulcs.
- A `[Sensible]` jelölésű tulajdonságok (jelszó-hash, token nonce-ok) `***` értékként tárolódnak.
- A végrehajtó (`UserId` / `BehalfOfId`) az `IAuditActorProvider`-ből származik: a `HttpAuditActorProvider` az aktuális kérés munkamenet-claimjéből oldja fel, a névtelen kéréseket felhasználó nélkül naplózza, a kérésen kívül futó kódot pedig a rendszerfelhasználóként naplózza.
- Megszemélyesítés: az `ISessionCommands.CreateImpersonation` hívással indított munkamenet a megszemélyesítőt az `ImpersonatorId` oszlopában tárolja. Az `ISessionQueries.TryFindActor` visszaadja a munkamenet felhasználóját és a megszemélyesítőt, az `AuditActor.ForSession` pedig ebből előállítja a végrehajtót: a megszemélyesítő a `UserId`, a megszemélyesített felhasználó a `BehalfOfId`. Egy ilyen munkamenet létrehozása `ImpersonationStarted` (a célszemély nevében), érvénytelenítése pedig `ImpersonationEnded` néven kerül az auditnaplóba a `Created` / `Updated` helyett.
- Maguk az `AuditLogEntry` sorok sosem kerülnek auditálásra, a szinkron `SaveChanges` pedig nem támogatott.
- A rendszer- és a vendégfelhasználót a migrátor hozza létre (`EnsureBuiltInUsersAsync`), az audit sorok `UserId` / `BehalfOfId` kulcsai pedig `NoAction` típusúak, így az audit sorok által hivatkozott felhasználó csak soft delete-tel törölhető; a hard delete-et az adatbázis elutasítja, hogy az auditnyom visszavezethető maradjon.

Ezekhez az entitásokhoz ne adjon hozzá kézzel `AuditLogEntry` sorokat.

## Soft delete, hard delete és a törölt rekordok láthatósága

Nincs `IsDeleted` oszlop. Egy rekord akkor törölt, ha az `EntityName` és az `EntityKey` értékéhez több `Deleted`, mint `Restored` `AuditLogEntry` sor tartozik.

- A `User`, `Email`, `Role`, `Grant`, `Password` és `WallOfShameEntry` soft delete-tel törölhető (`ApplySoftDelete` az `AbstractCoreContext`-ben). Eltávolításukkor a sor megmarad, a rendszer megírja a `Deleted` audit bejegyzést, egy globális lekérdezési szűrő (`SoftDelete`) pedig elrejti őket. A kaszkádolt függő rekordok ugyanígy törlődnek, a nem soft delete-elhető függő rekordok (munkamenetek) fizikailag törlődnek.
- A hard delete explicit: az `IUnitOfWork.GetHardDeleteRepository<TAggregate, TKey>()` az `IHardDeleteRepository`-t adja vissza a `HardDelete` / `HardDeleteAsync` metódusokkal. A sor (és a kaszkádolt függő rekordjai) véglegesen törlődik, az auditnapló pedig mindegyikükhöz `HardDeleted` bejegyzést kap, így az esemény nyomon követhető marad. Egy már soft delete-tel törölt rekord hard delete-je támogatott.
- A visszaállítás explicit: az `IUnitOfWork.GetRestoreRepository<TAggregate, TKey>()` az `IRestoreRepository`-t adja vissza a `RestoreAsync` metódussal. A rekordot és a soft delete-tel törölt kaszkádolt függő rekordjait megjelöli (`ISoftDeleteContext.Restores`), a következő `SaveChangesAsync` pedig mindegyikükhöz `Restored` audit bejegyzést ír, a korábbi `Deleted` bejegyzések megmaradnak. Egy nem törölt rekord visszaállítása nem csinál semmit.
- A soft delete az `AuditSaveChangesInterceptor`-ra épül; az nélküli kontextus fizikailag törli a sorokat.
- A `context.SoftDelete.IncludeDeleted()` egy `IDisposable` hatókört (`SoftDeleteSwitch`, szálbiztos és egymásba ágyazható) ad vissza, amely a megnyitott állapotában kikapcsolja a szűrőt az adott kontextuson. Az `AuditLogEntryQueries` ezt használja, hogy az audit bejegyzések mindig fel tudják oldani a felhasználóikat.
- Ha egy tárolt felhasználóazonosító (audit `UserId` / `BehalfOfId`, wall of shame `UserId` / `GivenById`, munkamenet `InvalidatedById`) nem oldható fel felhasználóra, például mert a felhasználó soft delete-tel törölt és a szűrő aktív, az entitás hiba helyett `UnknownUser(Id)` értéket kap (`UserResolutionExtensions.ToUser`).
- A `DeletedRecordsVisibilityMiddleware` megnyitja a hatókört a `ViewDeleted` jogosultsággal (`DomainConstants.ViewDeletedGrant`, az `IUserQueries.HasGrant` ellenőrzi) rendelkező, hitelesített felhasználók teljes kérésére, így ezek az adminisztrátorok a kérés minden lekérdezésében látják a törölt rekordokat.

### Kezelőlisták

Minden kezelőoldal (felhasználók, szerepkörök, oldalak) ugyanazt a felépítést követi: lapozott lista keresési kifejezéssel, kezdőbetű-csoporttal és törlési állapottal, `Delete` (soft), a fejlesztők (`RawDatabase.Manage`) számára pedig `deleted=true` listázás, `restore` és `hard/delete`.

- Az `InDeletionState` (`Behind.Common.Database`) azt szűri egy lekérdezésen, hogy az auditnapló szerint a sor törölt-e, még `IncludeDeleted` hatókörön belül is. A soft delete kulcsok az elsődleges kulcs értékeinek `:` jellel összefűzött szövege (ugyanaz a szöveg, mint az audit `EntityKey`), így az összetett kulcsú entitások, például a `Site` (`SiteId`, `Version`) is soft delete-elhetők, és minden sor (verzió) saját `Deleted` / `Restored` bejegyzést kap.
- Az `InBucket` / `LettersAsync` (`LetterBucketQueryExtensions`) bármely lekérdezéshez megvalósítja a betűszűrőt a címkekifejezés alapján.
- A hard delete parancsok `HardDeleteResult` választ adnak (`NotFound`, ha a rekord nem soft delete-tel törölt, `Rejected`, ha az adatbázis elutasítja; `404` / `409` kódra leképezve).
- Egy oldal törlése csak a `Sites` sorait törli soft delete-tel. Az `ActiveVersions` sor megmarad, így a visszaállítás visszahozza az élő verziót, a hard delete pedig azt is eltávolítja.

### Felhasználókezelés

- A `POST /users/{id}/delete` soft delete-tel töröl egy felhasználót (`User.Delete` jogosultság, ugyanazok a moderációs szabályok, mint a tiltásnál: nem törölheti önmagát, fejlesztőt csak fejlesztő törölhet). A felhasználó munkamenetei vele együtt törlődnek.
- A `GET /users?deleted=true`, a `POST /users/{id}/restore` és a `POST /users/{id}/hard/delete` csak fejlesztők számára érhető el. A visszaállítás és a hard delete csak a már soft delete-tel törölt felhasználókra vonatkozik (különben `404`), így a végleges törlés mindig egy második, explicit lépés.
- A hard delete `409` választ ad, ha az adatbázis elutasítja, például mert audit bejegyzések tartoznak a felhasználóhoz (lásd a fenti auditnaplóra vonatkozó megjegyzéseket).
- Az `IUserManagementQueries` egy `DeletionState` értéket (`Active` / `Deleted`) kap, és `Deleted` esetén maga nyitja meg az `IncludeDeleted` hatókört; az `Active` mindig kizárja a törölt felhasználókat, még a `DeletedRecordsVisibilityMiddleware` hatókörén belül is.

## Kétfaktoros hitelesítés

Három tábla tárolja a felhasználó második faktorát; mindegyik egyszerű entitás generált `Id`-val, a `Users` táblára mutató `UserId` idegen kulccsal (kaszkádolt törlés, így a felhasználó hard delete-je eltávolítja őket) és soft delete nélkül (egy eltávolított passkey vagy felhasznált helyreállítási kód végleg eltűnik, az auditnapló megőrzi, ki tette):

- `UserTotps` (`UserTotp`): `Secret` (Base32, `[Sensible]`), `IsConfirmed` (egy függőben lévő titok addig nem második faktor, amíg egy kódot meg nem erősítettek) és `LastUsedStep` (visszajátszás elleni védelem); felhasználónként egy sor (egyedi `UserId`).
- `UserPasskeys` (`UserPasskey`): `Name`, `CredentialId` (egyedi, base64url), `PublicKey` (base64url) és `SignCount`. A létrehozás idejét az auditnaplóból olvassa, sosem tárolja.
- `UserRecoveryCodes` (`UserRecoveryCode`): `Hash` (BCrypt, `[Sensible]`). A kód felhasználáskor törlődik.

Az `ITwoFactorQueries` / `ITwoFactorCommands` (`TwoFactorQueries`, `TwoFactorCommands`) olvassa és módosítja őket; a lépést és az aláírásszámlálót feltételes frissítésekkel léptetik, így a visszajátszás elveszti a versenyt. Szerepelnek az `AuditLabelExtensions`-ben („Authenticator app of ...”, „Passkey of ...”, „Recovery code of ...”), és szerkeszthetők a nyers adatbázis-szerkesztőben (a titkok maszkoltak). Migráció: `AddTwoFactor`.

## Felhasználói beállítások

A `UserSettings` tábla (`AddUserSettings` migráció) egyszerű, felhasználónkénti kulcs/érték párokat tárol: `UserId` (a `Users` táblára mutató idegen kulcs, kaszkádolt törlés), `Key` (legfeljebb 64) és `Value` (legfeljebb 256), az elsődleges kulcs a `UserId` + `Key`. A `UserGrants`-hoz hasonló megosztott típusú kapcsolótábla, nem auditált entitás, ezért az `IUserSettingQueries` / `IUserSettingCommands` olvassa és írja, és nincs audit címkéje. Az egyetlen eddigi kulcs a `locale` (`UserSettingKeys.Locale`).

## Verziózott oldalak

A `Site` az első verziózott entitás. Azonosítója egy `VersionedKey<Guid>` (`Behind.Common`, az `ActiveVersion` mellett a `Behind.Common.Database`-ben) (az oldal azonosítója és egy 1-től induló verziószám), a látogatók által látott verziót pedig egy `ActiveVersion` sor dönti el, sosem maga az oldal sora.

- A `Sites` összetett kulcsa (`SiteId`, `Version`). A domain `Site.Id`-t az EF figyelmen kívül hagyja, és a két shadow oszlopban tárolja, ahogy a többi entitás is megtartja az azonosítóit. A mentés sosem módosít sort: az `ISiteCommands.Create` / `SaveVersion` a `latest + 1` verziót szúrja be (ugyanannak a verziónak az egyidejű beszúrása a kulcson elbukik, és `SiteSaveResult.VersionConflict` eredményt ad).
- A `HideTitle` (`bool`, alapértelmezetten `false`) verziónként tárolódik: ha be van állítva, a nyilvános oldal a tartalmat (és a karusszelt) a címsora nélkül jeleníti meg.
- A `PublishedAt` (`Option<DateTimeOffset>`) a piszkozat jelző: a `None` piszkozatot jelent. A `PublishedAtUtc` shadow oszlopban tárolódik. A `Sites` sorain az egyetlen valaha végzett módosítás a `PublishedAtUtc` kitöltése, amikor egy piszkozatot az `ISiteCommands.Activate` élesít.
- Az `ActiveVersions` (`EntityType`, `EntityKey`, `Version`, kulcs: `EntityType` + `EntityKey`) tartja az élő verziót; az `EntityType` a `Site.EntityType`, az `EntityKey` pedig az oldal azonosítója. Általános tábla, ezért saját kontextusa van: az `AbstractVersioningContext` (`VersioningContextSqlite`) saját migrációkkal a `Migrations/Versioning` mappában és saját előzménytáblával (`__VersioningMigrationsHistory`). Az `ActiveVersion`, a leképezése és az `ActiveVersionsAsync` / `SetActiveVersionAsync` a `Behind.Common.Database`-ben található. A migrátor először a core, másodszor a verziózási migrációkat alkalmazza.
- A verziózási kontextus a core kontextushoz hasonlóan auditált (ugyanaz az `AuditSaveChangesInterceptor`): az `AuditLogEntry`-t a meglévő `AuditLogEntries` táblára képezi le `ExcludeFromMigrations` beállítással, így a migrációi sosem hozzák létre és nem módosítják azt a táblát. A közzététel vagy az aktiválás hozzáadja az `ActiveVersions` sort, vagy az `UpdateImmutable` segítségével lecseréli, így az auditnapló a `Version` változásával járó `Updated` bejegyzést mutat.
- A két kontextust egymás után mentik (közös tranzakció nélkül): a `SiteCommands` először a `Sites` sort, másodszor az `ActiveVersions` sort menti, így a közbeni hiba közzétett, de még nem élő verziót hagy maga után, sosem olyan élő verziót, amely nem létezik.
- Egy slug foglalt, ha másik oldalnak az élő vagy a legújabb verziójában szerepel (`IsSiteSlugTakenAsync`); ezt mentéskor és aktiváláskor ellenőrzi (`SiteSaveResult.SlugTaken`). Az `ISiteQueries.FindActiveBySlug` csak az élő verziók slugját oldja fel, a piszkozatok sosem szivárognak ki.
- A feltöltött oldalképek a VFS fájljai (`VfsFile`, `Sites` típus, lásd alább a „Virtuális fájlrendszer” részt). A `GET /sites/images/{id}` szolgálja ki őket immutable gyorsítótár-fejléccel.
- Audit címkék: az `AuditLabelExtensions` egy oldalt a legújabb verziójának címével nevez meg (a `Site` kulcsai `<id>:<version>` alakúak), egy képet pedig a fájlnevével.
- A tartalmat mentés előtt a `SanitizeRichText()` (`Behind.Common.WebApi`, HtmlSanitizer) megtisztítja; csak a szerkesztő címkéi, `class` értékei és az `http` / `https` / `mailto` hivatkozások maradnak meg.

A `Site` jogosultsági terület (`Site.Read`, `Write`, `Delete`, `Manage`) kizárólag adminisztrációs: a `Users` nem kapja meg a `Site.Read` jogosultságot.

## Hírek

A `News`, a `NewsCategory` és a `NewsComment` a `Site`-hoz hasonlóan verziózott entitás: az `Id`-juk egy `VersionedKey<Guid>` (`Behind.Common`), amelyet a `HasVersionedKey()` (`Behind.Common.Database.VersionedEntityExtensions`) shadow összetett kulcsként (`Key`, `Version`) tárol, és a `News`, `NewsCategories` és `NewsComments` táblákra képez le. A mentés a `latest + 1` verziót szúrja be, és sosem módosít sort (az egyetlen helyben végzett módosítás a kiemelés törlése, lásd alább). Mindhárom soft delete-elhető gyökér, minden verziósor saját `Deleted` / `Restored` audit bejegyzést kap (`<key>:<version>`). A közös segédek a `Behind.Common.Database`-ben találhatók: `OfKey`, `OfKeys`, `OfVersion`, `LatestOf` (minden kulcs legmagasabb verziója), `LatestVersionAsync`, `StampVersionedKey` / `VersionedId`, valamint a `MarkSoftDeletedAsync` / `MarkRestoredAsync` / `MarkHardDeletedAsync` / `HasDeletedRowsAsync`, amelyek egy életciklus-változást egy kulcs minden verziójára alkalmaznak.

- **Melyik verzió látszik.** A `News` pontosan úgy működik, mint a `Site`: egy verzió piszkozat, amíg élesre nem állítják. Az `ActiveVersions` (`EntityType` = `News.EntityType`, `EntityKey` = a hír kulcsa) az élő verzióra mutat, az `AbstractVersioningContext`-en keresztül mentve (`NewsCommands.Create` / `SaveVersion` `publish` értékkel, `Activate`). Az `AbstractCoreContext` az `ActiveVersion`-t is csak olvashatóként képezi le (`ExcludeFromMigrations`), így a core lekérdezések lapozás közben SQL-ben csatolhatják az élő verziót (`LiveIn`). A `NewsCategory`-nak és a `NewsComment`-nek nincs piszkozata: a legújabb verzió az aktuális.
- **Nincsenek idegen kulcsok a verziózott entitások között.** Egy idegen kulcs nem mutathat összetett kulcsra, ezért a `News.CategoryId` és a `NewsComment.NewsId` egyszerű kulcsoszlop (a szerző és a fejléckép valódi idegen kulcsot kap). A kaszkádokat a parancsok végzik: egy hír törlése, visszaállítása vagy hard delete-je a hír verzióira és a hír hozzászólásaira vonatkozik. A törölt kategória megtartja a híreit, amelyek kategória nélkül jelennek meg, mert a lekérdezések csak az aktív kategóriákat csatolják.
- A `News` (`Title`, `Summary`, `Content`, `StartAt`, `Highlighted`) megtartja az `AuthorId`-t (az 1. verzió szerzőjét minden későbbi verzió megőrzi), az opcionális `CategoryId`-t és az opcionális végpontot (`EndAt`, `EndAtUtc` shadow oszlop, a `Site.PublishedAt`-hoz hasonlóan). A `Summary` a hírlistában megjelenő rövid tartalom, a `Content` a hír megnyitásakor jelenik meg, mindkettőt a `SanitizeRichText()` tisztítja. Ha a tartalom üres, a megnyitott hír az összefoglalót mutatja (`NewsDetail.DisplayContent`, a válaszban `displayContent`), miközben a `content` érintetlen marad a szerkesztő számára.
- **A létrehozás és a módosítás ideje az auditnaplóból származik** (szabály az `AGENTS.md`-ben): az `AuditTimeExtensions.AuditTimesAsync<T>` minden kulcsnak a megjelenített verzióját veszi, és az 1. verzió `Created` bejegyzésének idejét adja vissza létrehozási időként, illetve – ha a megjelenített verzió 1-nél későbbi – a megjelenített verzió `Created` bejegyzésének idejét módosítási időként. A `VersionCreationTimesAsync` megadja az egyes verziók idejét (a szerkesztő verziólistája), az `OrderByCreation` / `ThenByCreation` pedig SQL-ben az 1. verzió létrehozása szerint rendez egy lekérdezést. Az audit interceptor nélküli kontextuson keresztül írt sorokhoz nem tartozik idő.
- **Ütemezés (opcionális).** Egy verziónak alapértelmezetten nincs ütemezése (se kezdet, se vég: amint élő, látszik, amíg törlik). Az ütemezésnek kötelező `StartAt` és opcionális `EndAt` értéke van, mindkettő `Option<DateTimeOffset>`, a nullable `StartAtUtc` / `EndAtUtc` shadow oszlopokban tárolva (a `Site.PublishedAt`-hoz hasonlóan). Egy hír addig látszik, amíg az élő verziójának nincs kezdete vagy `StartAt <= now`, és nincs vége vagy `EndAt > now` (`NewsScheduleExtensions.StatusAt`, `ActiveAt`). A kezdet nélküli vég, illetve a kezdet után nem következő vég elutasításra kerül (`NewsSaveResult.InvalidSchedule`, `409` `invalid-schedule`, `IsValidSchedule`). A nyilvános lista, a nyilvános `Find(id, now)`, a hozzászólások listája és a `NewsCommentCommands.Add` (amelynek szüksége van a `TimeProvider`-re) csak az élő híreket látja. A `ListManaged`, a `FindHistory` és a `FindVersion` nem szűr időre vagy élő állapotra. A listák a kezdési idő szerint rendeznek, ha van, különben a létrehozási idő szerint (az `OrderByCreation(..., overriddenBy)` SQL-ben összevonja a kettőt), a nyilvános lista a kiemelt híreket teszi előre.
- **Kiemelés.** Egyszerre csak egy hír lehet kiemelt: egy hír kiemelése az élő verziója `Highlighted` jelzője. Egy kiemelt verzió közzététele vagy aktiválása törli a többi élő, kiemelt verzió jelzőjét (ezeknek a soroknak a helyben végzett, auditált módosítása, új verzió nélkül), egy nem kiemelt verzió közzététele megszünteti az adott hír kiemelését, a `Delete` pedig először törli a jelzőt, így egy törölt hír sosem tartja a kiemelést, és a visszaállítás sem hozhat vissza egy másodikat. Erre nincs adatbázis-megszorítás, a szabályt a parancsok tartják be.
- A `NewsCategory` (`Name`) megtartja az opcionális `HeaderImageId`-t (`SetNull` a `SiteImages`-re). A nevek az aktív kategóriák között kis- és nagybetűk figyelmen kívül hagyásával egyediek (`NewsCategorySaveResult.NameTaken`), és az olyan kategória visszaállítása, amelynek a nevét közben lefoglalták, elutasításra kerül.
- A `NewsComment` (`Content`, legfeljebb 2000 karakter) megtartja a `NewsId`-t és az `AuthorId`-t. Egy hozzászólás szerkesztése (`INewsCommentCommands.Update`) ugyanazzal a hírrel és szerzővel szúrja be a következő verziót, így a szerkesztési előzmény megmarad, a „szerkesztve” időpont pedig a megjelenített verzió létrehozási ideje. Azt, hogy ki szerkeszthet, a `NewsCommentEndpoints` dönti el: a szerző vagy a `News.Manage` (`EditGrant`) birtokosa; egy jövőbeli moderátori szerepkörnek csak ezt a jogosultságot kell megadni.
- A nyilvános lekérdezések (`INewsQueries.List` / `Find`, `INewsCommentQueries`, `INewsCategoryQueries.ListActive`) csak az aktív (nem soft delete-tel törölt) sorokat olvassák, így a fejlesztők, akiknek a kérései tartalmazzák a törölt rekordokat, sosem látnak törölt sorokat a nyilvános oldalakon. A szerzőket, kategóriákat és hozzászólásszámokat oldalanként egyetlen lekérdezés oldja fel.
- A fejléc- és tartalomképek VFS-fájlok (a `POST /news/images` a `SiteImageEndpoints.UploadNews`, a csatolásig `News` típussal tárolva, a `News.Write` jogosultság mögött; a képeket a `GET /news/images/{id}` szolgálja ki).
- Audit címkék: az `AuditLabelExtensions` egy hírt a legújabb verziójának címével, egy kategóriát a nevével, egy hozzászólást pedig `News comment of <author>` formában nevez meg; az audit kulcsok `<id>:<version>` alakúak, így a `LabelKey` `Title: 2` értéket mutat.
- Migrációk: a `MakeNewsVersioned` az `Id`-t `Key`-re nevezi át, hozzáadja a `Version` oszlopot (a meglévő sorok az 1. verzióvá válnak), eldobja a hírtáblák közötti idegen kulcsokat, és az audit kulcsokat `<id>:1` alakra írja át; a `PublishExistingNews` (verziózási kontextus) élesíti a meglévő híreket.

A `News` jogosultsági terület (`News.Read`, `Write`, `Delete`, `Manage`) kizárólag adminisztrációs: a `Users` nem kapja meg a `News.Read` jogosultságot. A hírek olvasása nyilvános, a hozzászóláshoz bejelentkezett munkamenet szükséges, egy hozzászólás törléséhez pedig a szerzőnek kell lenni, vagy rendelkezni kell a `News.Delete` jogosultsággal.

## Cikkek

Az `Article` és az `ArticleCategory` pontosan úgy verziózott entitás, mint a `News` és a `NewsCategory` (`VersionedKey<Guid>` azonosító, piszkozatok, `ActiveVersions` az `Article.EntityType` értékkel, soft delete, hard delete, visszaállítás), a következő eltérésekkel: a cikknek csak `Title`, `Summary` és `Content` mezője van (plusz a shadow `AuthorId` és `CategoryId`), nincs ütemezése, kiemelése és hozzászólása, valamint saját kategóriái vannak (`ArticleCategory`, `Name`, opcionális `HeaderImageId`), amelyek függetlenek a hírkategóriáktól.

- Keresés: az `ArticleQueryExtensions.Matching` a kisbetűsített cím vagy tartalom alapján szűr (az `IArticleQueries.List` az élő verziókban keres, a `ListManaged` és a `GetLetters` a legújabb verziókban).
- Audit címkék: az `AuditLabelExtensions` egy cikket a legújabb verziójának címével, egy cikkkategóriát pedig a nevével nevez meg.
- Migráció: az `AddArticles` létrehozza az `Articles` és az `ArticleCategories` táblát.

Az `Article` jogosultsági terület (`Article.Read`, `Write`, `Delete`, `Manage`) a `News`-hoz hasonlóan kizárólag adminisztrációs: a `Users` nem kapja meg az `Article.Read` jogosultságot. A cikkek olvasása nyilvános.

## Galériák

A `Gallery` és a `GalleryItem` az `Article`-höz hasonlóan verziózott entitás (`VersionedKey<Guid>` azonosító, piszkozatok az `ActiveVersions`-en keresztül a `Gallery.EntityType` értékkel, soft delete, hard delete, visszaállítás).

- `Gallery`: `Title`, `Description`, plusz a shadow `AuthorId`, `ThumbnailImageId` (opcionális `VfsFile` kulcs, a feltöltött vagy generált galériaminiatűr) és `ThumbnailGenerated` (a miniatűr minden mentéskor újragenerálódik az első elemekből).
- `GalleryItem`: `Description`, plusz a shadow `GalleryId` (a galéria kulcsa, nincs idegen kulcs, mert a kulcsok összetettek), `ImageId` és `ThumbnailId` (mindkettő a `Galleries` típusú `VfsFile` kulcsa) és `Position`. Ezen kulcsok egyikéhez sincs adatbázis-idegenkulcs, mert a `VfsFile` kulcsa összetett; a `GalleryCommands` ellenőrzi, hogy minden hivatkozott kép létezik a `Galleries` típussal. Egy elem verziója annak a galériának a verziója, amelyhez tartozik: a galéria minden mentése beszúrja az új galériasort és elemenként egy friss sort, így egy verzió önmagában teljes, egy régebbi verzió aktiválása pedig pontosan annak a képeit mutatja. Az elemek önmagukban nem törölhetők soft delete-tel; a `Gallery` soft delete-re van regisztrálva, a galéria hard delete-je pedig az összes elemét eltávolítja.
- Az elemleírások opcionálisak: üresek lehetnek a piszkozatokban és a közzétett verziókban is.
- Miniatűrök: a `Behind.Common` (`ImageThumbnailExtensions`, SkiaSharp) dekódolja a képet, alkalmazza az EXIF tájolást, lekicsinyíti, és WebP formátumba kódolja; az eredeti kép és a miniatűrje `GalleryImage` sorokként tárolódik.
- A galériaképek a `Galleries` VFS típus fájljai (saját feltöltési végpontok és saját méretkorlát, de a közös `VfsFile` tároló). Nem törölhetők soft delete-tel; egy galéria hard delete-je törli azokat a fájlokat, amelyeket csak ez a galéria használt (az elemképeit, elemminiatűrjeit és a galériaminiatűröket az `IVfsCommands.Delete` segítségével, amely a tartalmat is eltávolítja), és megtartja azokat, amelyekre egy másik galéria még hivatkozik. Egy galéria mentése csatolja hozzá a képeit (`IVfsCommands.Attach`, lásd alább). Azok a feltöltések, amelyeket sosem mentettek galériába, a VFS-ben maradnak.
- Audit címkék: az `AuditLabelExtensions` egy galériát a legújabb verziójának címével, egy elemet a leírásával (80 karakterre rövidítve, `Image`, ha nincs neki) és egy VFS-fájlt (oldal- vagy galériakép) a fájlnevével nevez meg.
- Migrációk: az `AddGalleries` létrehozza a `Galleries` és a `GalleryItems` táblát, a `GalleriesOwnImages` létrehozza a `GalleryImages` táblát, és eldobja a `SiteImages`-re mutató idegen kulcsokat; mindkét képtáblát a VFS váltja fel, lásd alább.

A `Gallery` jogosultsági terület (`Gallery.Read`, `Write`, `Delete`, `Manage`) az `Article`-höz hasonlóan kizárólag adminisztrációs. A beépített főmenünek van egy `Galleries` lapja (`#/galleries`).

## Oldalszekciók

A `SiteSection` verziózott entitás (`VersionedKey<Guid>` azonosító), amely egy oldalt szekció alapú oldallá alakít, például egy nyitóoldallá, ahol a szövegek mellett képek is vannak. A szekciók egy oldal egy verziójához tartoznak, és a tartalma alatt jelennek meg.

- `SiteSection` (`SiteSections`): `Theme` (`SiteSectionTheme`: `Normal` vagy `Accented`, szövegként tárolva), `Title` (legfeljebb 200 karakter, lehet üres), `Content` (megtisztított formázott szöveg, legfeljebb 100000 karakter, lehet üres), `ImageAlt` (legfeljebb 300 karakter) és `ImagePosition` (`SiteSectionImagePosition`: `Left` vagy `Right`, szövegként tárolva), plusz a `SiteId`, `ImageId` (nullable, a `Sites` típusú `VfsFile` kulcsa, nincs idegen kulcs) és `Position` shadow oszlopok. Egy szekcióhoz cím, szöveg vagy kép szükséges.
- A szekció kulcsa egy friss uuid, a verziója pedig annak az oldalnak a verziója, amelyhez tartozik. A `GalleryItem`-hez hasonlóan minden `ISiteCommands.Create` / `SaveVersion` szekciónként egy friss sort szúr be az új oldalverzióval együtt, így minden verzió önmagában teljes, a régebbi verziók megtartják a szekcióikat, a szekciók nélkül mentett verziónak pedig nincs szekciója. Egy oldalnak legfeljebb `Site.MaxSections` (30) szekciója lehet.
- A szekciók önmagukban nem törölhetők soft delete-tel: követik az oldalt. Egy oldal `Delete` és `Restore` művelete a helyükön hagyja őket (csak egy látható oldalverzión keresztül olvashatók), a `HardDelete` pedig az oldallal együtt eltávolítja a törölt verziók szekcióit.
- A `SaveVersion` és a `Create` ellenőrzi, hogy minden `ImageId` a `Sites` VFS típus létező fájlja-e, ellenkező esetben `SiteSaveResult.UnknownImage` választ ad (semmi sem mentődik). A mentés után a szekcióképek és a szekciótartalomba beágyazott képek az oldalhoz csatolódnak (`IVfsCommands.Attach`, a tartalom a `vfs/Sites/<site uuid>/<image uuid>` alatt), pontosan úgy, mint az oldaltartalom képei. A szekcióképeket az oldalkép-végponttal (`POST /sites/images`) tölti fel, és a `/sites/images/<id>` címről szolgálja ki.
- Olvasás: az `ISiteQueries.FindSections(VersionedKey<Guid>)` egy oldalverzió szekcióit adja vissza sorrendben. A nyilvános oldal (`GET /sites/slug/{slug}`, `GET /sites/index`) és a szerkesztő (`GET /sites/{id}/versions/{version}`) egyaránt tartalmazza őket.
- Audit címkék: egy szekciót a címe nevez meg (`Section`, ha nincs neki), az entitásnév `SiteSection`.
- Nyers adatbázis-szerkesztő: a `SiteSections` összetett kulcsú és nincs érzékeny oszlopa, így külön regisztráció nélkül szerkeszthető.
- Migráció: az `AddSiteSections` létrehozza a `SiteSections` táblát.

## Oldalkarusszelek

A `Carousel` és a `CarouselSlide` verziózott entitás (`VersionedKey<Guid>` azonosító, a mentés a következő verziót szúrja be és sosem módosít sort, soft delete, hard delete, visszaállítás), amely egy oldalnak opcionális képcsúszkát ad.

- `Carousel` (`Carousels`): `IntervalSeconds` (a beállított tartományon belül). A karusszel kulcsa az oldalának kulcsa (nincs idegen kulcs, mert a kulcsok összetettek), így egy oldalnak legfeljebb egy karusszelje van, és minden mentése egy verziót ad hozzá. A mentés élővé teszi az új verziót (`ActiveVersions` a `Carousel.EntityType` értékkel), piszkozatok nincsenek.
- `CarouselSlide` (`CarouselSlides`): `Caption` (legfeljebb 1000 karakter, a beállított korlát ennél kisebb vagy egyenlő) és `LinkUrl` (mindkettő lehet üres), plusz a `CarouselId` (az oldal kulcsa), `ImageId` (a `Carousels` típusú `VfsFile` kulcsa, nincs idegen kulcs) és `Position` shadow oszlopok. A `GalleryItem`-hez hasonlóan minden mentés beszúrja az új karusszelsort és diánként egy friss sort, így egy verzió önmagában teljes. A diák önmagukban nem törölhetők soft delete-tel.
- A `CarouselCommands.Save` nem törölt oldalt igényel, és ellenőrzi, hogy minden kép létezik a `Carousels` VFS típussal. A következő verziót az összes sor – a törölteket is beleértve – alapján számítja, így egy karusszel eltávolítása után újra létrehozható.
- Törlés: a `Delete` minden verziót soft delete-tel töröl, a `Restore` visszahozza a törölt verziókat, a `HardDelete` (csak törölt sorokra) pedig eltávolítja őket a diáikkal és azokkal a képekkel együtt, amelyeket egyetlen másik dia sem használ, és törli az aktív verziót, ha egyetlen verzió sem marad. A `SiteCommands` ezeket hívja, amikor egy oldalt töröl, visszaállít vagy véglegesen töröl, így a karusszel követi az oldalát.
- Nyilvános olvasás: az `ICarouselQueries.FindLive` csak olyan oldalra válaszol, amelynek van élő verziója, így egy piszkozat vagy törölt oldal karusszelje sosem kerül kiszolgálásra. A kezelői olvasás (`FindManaged`) a kért törlési állapotú legújabb karusszelt adja vissza.
- Képek: a `Carousels` VFS típusnak saját fajtája (`VfsTypes.CarouselImages`), saját URL-je (`/carousels/images/<id>`) és saját feltöltési végpontja van. A mentés az oldalhoz csatolja a képeket (`IVfsCommands.Attach`, a tartalom a `vfs/Carousels/<site uuid>/<image uuid>` alatt).
- Oldalanként opcionális: egy oldalnak csak akkor van karusszelje, ha mentettek egyet, a nélküle lévő oldal pontosan úgy szolgálódik ki, mint korábban. A korlátok (diák, intervallumtartomány, felirathossz, képméret) konfigurációból származnak (`Frontend:Carousels`, `CarouselsConfiguration`), és a végpont ellenőrzi őket, lásd a webes API konvencióit; az adatbázis csak az értékeket tárolja.
- Audit címkék: a karusszel kulcsa az oldal kulcsa, így az auditnapló az oldal címét mutatja (`Carousel` entitásnév); egy diát a felirata nevez meg (`Image`, ha nincs neki).
- Nyers adatbázis-szerkesztő: mindkét tábla szerkeszthető, mint bármely más tábla, nincs szükség regisztrációra.
- Migráció: az `AddCarousels` létrehozza a `Carousels` és a `CarouselSlides` táblát.

## Letöltések

A `DownloadFolder` és a `Download` a `Menu`-höz hasonlóan verziózott entitás (`VersionedKey<Guid>` azonosító, a mentés a következő verziót szúrja be és sosem módosít sort, nincsenek piszkozatok, soft delete, hard delete, visszaállítás).

- `DownloadFolder` (`DownloadFolders`): `Slug` (egy slug, az aktív testvérek között kis- és nagybetűk figyelmen kívül hagyásával egyedi, a `#/downloads/<slug>/<slug>` útvonalban használatos) és `DisplayName`, plusz a `ParentId` shadow oszlop (a szülőmappa kulcsa, legfelül `null`, nincs idegen kulcs, mert a kulcsok összetettek). Egy mappa szülője nem változik.
- `Download` (`Downloads`): `DisplayName` (alapértelmezetten a fájlnév), plusz a `FolderId`, `FileId` (a `VfsFile` kulcsa, nincs idegen kulcs) és `UploadedById` (idegen kulcs az emberi felhasználóra) shadow oszlopok. A méret, a fájlnév és a tartalomtípus a `VfsFile`-ból származik, a feltöltés ideje az auditnapló létrehozási bejegyzése (`AuditTimesAsync<Download>`), sosem oszlop.
- A fájl a `Downloads` VFS típus fájlja. A `DownloadCommands.Add` eltárolja a fájlt (`IVfsCommands.Add`), beszúrja a letöltést, majd csatolja hozzá a fájlt (`IVfsCommands.Attach`, a tartalom a `vfs/Downloads/<download uuid>/<file uuid>` alá kerül); egy sikertelen mentés újra eltávolítja a fájlt.
- Törlés: a törlés soft delete. Egy törölt mappa elrejt mindent, ami alatta van, mert a mappafa a felülről elérhető aktív mappákból épül fel (`DownloadQueryExtensions.Reachable`), így az almappák és a fájlok érintetlenek maradnak, és a mappával együtt jönnek vissza. Egy mappa visszaállításához aktív szülő és szabad slug kell, egy letöltés visszaállításához aktív mappa. Egy törölt mappa hard delete-je eltávolítja az almappáit és azok összes letöltését, és törli a VFS-fájlokat (az `IVfsCommands.Delete` a tartalmat is eltávolítja); egy letöltés hard delete-je törli a fájlját.
- Audit címkék: az `AuditLabelExtensions` egy mappát és egy letöltést a legújabb verzió megjelenített nevével nevez meg.
- Migráció: az `AddDownloads` létrehozza a `DownloadFolders` és a `Downloads` táblát.

A `Download` jogosultsági terület (`Download.Read`, `Write`, `Delete`, `Manage`) a `Gallery`-hez hasonlóan kizárólag adminisztrációs: a `Read` megnyitja az adminisztrációs oldal Letöltések eszközét, a `Write` mappákat hoz létre, fájlokat tölt fel és nevez át, a `Delete` törli őket. A fájlok böngészése és letöltése nyilvános. A beépített főmenünek van egy `Downloads` lapja (`#/downloads`).

## Virtuális fájlrendszer

Minden feltöltött elem egyetlen virtuális fájlrendszer (VFS) fájlja, a saját táblájának blob oszlopa helyett:

- A `VfsFile` (`VfsFiles` tábla) tárolja a metaadatokat: egy `VersionedKey<Guid>` azonosító (a sorok sosem módosulnak, egy változás a következő verziót szúrja be), a `Type` (annak az elemnek a fajtája, amelyhez a fájl tartozik: `Sites`, `News`, `Articles`, `NewsCategories`, `ArticleCategories`, `Galleries` vagy `Downloads`, lásd `VfsTypes`), a `FileName`, a felismert `ContentType`, a `Length` és az opcionális `ParentId`, annak az elemnek a kulcsa, amelyhez a fájl tartozik (nincs idegen kulcs). Nincsenek időoszlopai: a létrehozás ideje az audit bejegyzés, a fájl audit címkéje pedig a fájlneve.
- A tartalom nincs az adatbázisban. Az `IContentStore` (`Behind.Common.Storage`, megvalósítása a `DirectoryContentStore`) atomikusan írja a `<DataDirectory>/vfs` alá, és minden kulcsról ellenőrzi, hogy a gyökéren belül marad. Az elrendezés `vfs/<Type>/<parent uuid>/<file uuid>` a szülővel rendelkező fájlnál és `vfs/<Type>/<file uuid>` a szülő nélküli fájlnál (`VfsFile.StorageKey`, például `vfs/Articles/<article uuid>/<file uuid>` vagy `vfs/Sites/<file uuid>`). Az útvonal nem tartalmazza a verziót.
- A `DataDirectory` a `backend.json` fájlból olvasódik (`BackendConfiguration`, `Backend` szakasz, például `{ "Backend": { "DataDirectory": "/app/data" } }`). A fájl kötelező, és az alkalmazás indításkor hibával leáll, ha a `Backend:DataDirectory` hiányzik (`AddVfsContentStore`). A webalkalmazásnak és a migrátornak ugyanarra a könyvtárra kell mutatnia, a könyvtárról pedig az adatbázissal együtt kell biztonsági mentést készíteni.
- Az `IVfsCommands.Add` először a tartalmat, majd a sort írja (egy sikertelen mentés újra eltávolítja a tartalmat), és a fájlnak még nincs szülője. Az `IVfsCommands.Attach(type, parentId, keys)` akkor hívódik, amikor a szülő ismertté válik: átmozgatja a tartalmat a `<type>/<parent uuid>` mappába, és beszúrja a fájl következő verzióját a tulajdonos `Type` és a `ParentId` értékével (a már szülővel rendelkező fájlokat békén hagyja, így az első szülő nyer; egy sikertelen mentés visszamozgatja a tartalmat). A `GalleryCommands` minden mentés után a galéria kulcsához csatolja az elemképeket, az elemminiatűröket és a galéria miniatűrjét, az oldalképeket használók pedig szintén minden mentés után csatolják a sajátjukat (az `IVfsCommands.AttachSiteImagesOf` a `ReferencedKeys` segítségével találja meg a `/sites/images/<id>` URL-eket): a `SiteCommands` (tartalom), a `NewsCommands` és az `ArticleCommands` (összefoglaló és tartalom) az oldal, hír vagy cikk kulcsához csatol, a hír- és cikkkategória-parancsok pedig a fejlécképüket a kategória kulcsához csatolják. Egy oldalkép így a `vfs/<Sites|News|Articles|NewsCategories|ArticleCategories>/<parent uuid>/<file uuid>` helyen köt ki; egy még nem használt feltöltés annak a végpontnak a mappájában marad, amelyre feltöltötték (`/sites/images`: `Sites`, `/news/images`: `News`, `/articles/images`: `Articles`). Csak azonos fajtájú fájlok csatolódnak: az oldalkép-típusok (`VfsTypes.SameKindAs`) sosem keverednek a galériaképekkel. Az `IVfsCommands.Delete` az összes verziót és a tartalmat eltávolítja. Az `IVfsQueries.Open(types, key)` a legújabb verziót adja vissza a bájtokkal együtt, és csak a megadott típusú fájlokat találja meg. Minden tartalomfajta a képeit a saját URL-je alatt szolgálja ki: `GET /sites/images/{id}` (`Sites`), `GET /news/images/{id}` (`News`, `NewsCategories`), `GET /articles/images/{id}` (`Articles`, `ArticleCategories`) és `GET /galleries/images/{id}` (`Galleries`), a feltöltési végpontok pedig a saját tartalmuk URL-jével válaszolnak (`VfsTypes.ImageUrlOf`). A hír- és cikktartalom a képeire a saját URL-jével hivatkozik, a `ReferencedImageUrlsOf` a régebbi tartalmakban tárolt régi `/sites/images/<id>` URL-eket is elfogadja, a `GET /sites/images/{id}` pedig továbbra is kiszolgálja az összes oldalkép-típust, így a régi URL-ek tovább működnek, egy galériakép viszont sosem szolgálódik ki oldalképként. A tartalom nélküli sorra a válasz: nem található.
- A kapcsolódó táblák egyszerű `Guid` hivatkozásokat tartanak idegen kulcsok nélkül (`NewsCategories.HeaderImageId`, `ArticleCategories.HeaderImageId`, `GalleryItems.ImageId`, `GalleryItems.ThumbnailId`, `Galleries.ThumbnailImageId`), mert a kulcs összetett; a parancsok ellenőrzik, hogy a fájl létezik a megfelelő könyvtárban.
- Migrációk: az `AddVfsFiles` létrehozza a `VfsFiles` táblát, átmásolja a `SiteImages` metaadatait (a típus és a szülő az a oldal, hír, cikk vagy kategória, amely a képet használja, különben `Sites` szülő nélkül) és a `GalleryImages` metaadatait (`Galleries` típus, a szülő az a galéria, amelynek elemei vagy miniatűrje a képet használja, ha van ilyen; ugyanazok a kulcsok, 1. verzió), és eldobja a kategóriatáblák idegen kulcsait; a `DropLegacyImages` eldobja a két régi táblát. A blobokat nem lehet SQL-lel átmozgatni, ezért a migrátor a `MigrateWithVfsAsync`-et futtatja: ha a `DropLegacyImages` függőben van, az `AddVfsFiles`-ig migrál, a régi blobokat a `StorageKey` helyükön a tartalomtárba írja, majd alkalmazza a hátralévő migrációkat. Az idegen kulcsokat nem szabad ugyanabban a migrációban eldobni, amelyben a régi táblát, mert az SQLite a tábla eldobásakor nullázná a fejléckepeket.

## Menük

A `Menu` és a `MenuItem` verziózott entitás (`VersionedKey<Guid>` azonosító, a mentés a következő verziót szúrja be és sosem módosít sort, soft delete, hard delete, visszaállítás) piszkozatok nélkül: a legújabb verzió az aktuális, a kategóriákhoz hasonlóan.

- `Menu`: `Name` (egy slug, az aktív menük között kis- és nagybetűk figyelmen kívül hagyásával egyedi, a menü lekérésére használatos) és `Title`. A beépített `main` menü (`BuiltInMenus.Main`, fix azonosítók) nem nevezhető át, nem törölhető és nem törölhető véglegesen.
- `MenuItem`: `Title`, `Url`, `Target` (`CurrentPage`, `NewTab`, `NewWindow`, szövegként tárolva), `Kind` (`Link`, `User`) és `Position`, plusz a `MenuId` és `ParentId` shadow oszlopok (kulcsok, nem verziók, nincs idegen kulcs, mert a kulcsok összetettek). A `ParentId` az elemet almenüelemmé teszi: a szülőnek ugyanazon menü aktív, legfelső szintű `Link` eleme kell lennie, az almenüelemek egy szint mélyek. A `User` elemek a felhasználói menü bejegyzései, csak a főmenü legfelső szintjén engedélyezettek (a `MenuItemKind`-ot a `MenuItemCommands` ellenőrzi).
- Láthatóság: a közös `MenuItemRoles` kapcsolóentitás (`MenuItemKey`, `MenuItemVersion`, `RoleId`, kaszkádolt törlés az elemverzióval, nincs adatbázis-idegenkulcs a szerepkörökre, mert a `Role` konkrét típusonkénti táblára van leképezve) tárolja minden elemverzió szerepköreit. Ha nincs sor, az minden szerepkört és a nem bejelentkezett látogatókat jelenti. Azt, hogy mit lát a megtekintő, a `MenuVisibilityExtensions.IsVisibleTo` és a `MenuTreeExtensions.ToVisibleTree` dönti el; az almenüelem a szülőjével együtt rejtett. Egy szerepkör soft delete-je a szerepkör elemeit mindenki számára láthatatlanná teszi a szerepkör visszaállításáig.
- Törlés: egy menü vagy elem törlése soft delete, és az elemet az almenüelemeivel együtt elrejti; egy menü hard delete-je eltávolítja az összes elemét, egy elem hard delete-je pedig az almenüelemeit.
- Kezdeti adatok: az `EnsureBuiltInMenusAsync` (a migrátor hívja a beépített szerepkörök után) egyszer létrehozza a főmenüt a lapokkal (`Index`, `News`, `Articles`, `Galleries`, `Downloads`, `Wall of shame`) és a felhasználói menü elemeit (`Profile`, `Session`, `Sessions`, valamint az `Administrators` és `Developers` szerepkörnek az `Administration`); az adminisztrátor által törölt vagy módosított elemeket nem hozza létre újra.
- Audit címkék: az `AuditLabelExtensions` egy menüt a legújabb verziójának címével, egy menüelemet a címével nevez meg, így a `MenuItemRoles` audit kulcsai az elem és a szerepkör nevét mutatják.
- Migráció: az `AddMenus` létrehozza a `Menus`, a `MenuItems` és a `MenuItemRoles` táblát.

A `Menu` jogosultsági terület (`Menu.Read`, `Write`, `Delete`, `Manage`) kizárólag adminisztrációs: a `Users` nem kapja meg a `Menu.Read` jogosultságot. Egy menü feloldása az oldal számára nyilvános.

## Útvonal-aliasok

A `RouteAlias` a `Menu`-höz hasonlóan verziózott entitás (`VersionedKey<Guid>` azonosító, a mentés a következő verziót szúrja be és sosem módosít sort, soft delete, hard delete, visszaállítás). Egy rövid útvonalat képez le az alkalmazás egy oldalára, például a `manuals`-t a `#/downloads/products/manuals`-ra.

- Oszlopok: `Alias` (legfeljebb 100 karakteres slug, az aktív útvonal-aliasok között egyedi, indexelt) és `Target` (legfeljebb 2000 karakter, a `RouteTargetExtensions.IsValidRouteTarget` validálja: `#/`-sel kezdődik, és nem tartalmaz szóközt, vezérlőkaraktert vagy visszaperjelet).
- Lekérdezések: az `IRouteAliasQueries.FindTarget` egy aktív alias legújabb verziójának célját adja vissza; a kezelői lista lapozott, kereséssel, betűszűrővel és törlési állapottal.
- Audit címkék: az `AuditLabelExtensions` egy útvonal-aliast a legújabb verziójának aliasával nevez meg, törölt állapotban is.
- Migráció: az `AddRouteAliases` létrehozza a `RouteAliases` táblát.

A `RouteAlias` jogosultsági terület (`RouteAlias.Read`, `Write`, `Delete`, `Manage`) a `Menu`-höz hasonlóan kizárólag adminisztrációs. Egy alias feloldása nyilvános. A tábla a nyers adatbázis-szerkesztőben szerkeszthető, mint bármely más tábla.

## Hozzáférési beállítások és felhasználói jóváhagyások

Az `AccessSettings` verziózott singleton (`VersionedKey<Guid>` azonosító a fix `AccessSettings.SingletonKey` kulccsal, a mentés a következő verziót szúrja be és sosem módosít sort, soft delete, hard delete, visszaállítás). Ez tartalmazza a korlátozó oldalt (`RestrictionEnabled`, `RestrictedToRole`, `RestrictionTitle`, `RestrictionMessage`) és a `ModeratedSignUp` beállítást. Aktív sorok nélkül az `AccessSettings.Open` érvényes (minden kikapcsolva).

- `AccessSettings` tábla: `Key`, `Version`, `RestrictionEnabled`, `RestrictedToRole` (legfeljebb 100 karakter), `RestrictionTitle` (legfeljebb 200 karakter, az üres az alapértelmezett címsort jelenti), `RestrictionMessage` (megtisztított formázott szöveg, legfeljebb 20000 karakter), `ModeratedSignUp`.
- Lekérdezések: az `IAccessSettingsQueries.Current` a legújabb aktív verziót adja vissza (vagy az `Open` értéket), a `FindDeleted` a legújabb törölt verziót, amíg nincs aktív.
- Audit címkék: az `AuditLabelExtensions` az entitást `Access settings` néven nevezi meg.
- Képek: az üzenet képeket tartalmazhat. Ezek az `AccessSettings` VFS típus fájljai saját fajtával (`VfsTypes.AccessImages`), saját URL-lel (`/access/images/<id>`) és saját feltöltési végponttal. A mentés az üzenet által hivatkozott képeket a singleton kulcshoz csatolja (`IVfsCommands.Attach`), így a tartalom a `vfs/AccessSettings/<singleton key>/<file uuid>` alá kerül.
- `PendingUserApprovals` tábla (`UserId`, a felhasználóra mutató idegen kulcs kaszkádolt törléssel): egy sor azt jelenti, hogy a felhasználó jóváhagyásra vár, sor nélkül a felhasználó bejelentkezhet. A meglévő felhasználók ezért engedélyezettek maradnak. Az `IUserApprovalCommands.RequireApproval` hozzáadja a sort (regisztráció bekapcsolt moderáció mellett), az `Approve` eltávolítja; az `IUserApprovalQueries` az `IsPending` értékkel és a lapozott függőben lévők listájával válaszol (a soft delete-tel törölt felhasználók kimaradnak). Az `IdentityStore` ebből tölti ki az `ApplicationUser.PendingApproval` értékét.
- Migrációk: `AddAccessSettingsAndUserApprovals` és `AddAccessRestrictionTitle` (SQLite és PostgreSQL).

Az `Access` jogosultsági terület (`Access.Read`, `Write`, `Delete`, `Manage`) kizárólag adminisztrációs. Az `Access.Manage` a felhasználót a korlátozó oldal alól is mentesíti (`AccessSettingsExtensions.IsRestrictedFor`). Mindkét tábla szerkeszthető a nyers adatbázis-szerkesztőben, mint bármely más tábla.

## Nyers adatbázis-szerkesztők

A fejlesztők (`RawDatabase.Manage`) minden tábla sorait úgy szerkeszthetik, ahogyan tárolódnak (`#/manage/raw-database`, lásd `WEBAPI_CONVENTIONS.md`). A szerkesztő általános, és az EF Core modell vezérli, így egy új entitás megjelenéséhez nincs szükség extra kódra:

- A `DbContext.RawTables` (`Behind.Common.Database.Raw.RawTableExtensions`) a relációs modell minden elsődleges kulccsal rendelkező táblájából `RawTable`-t készít `RawColumn`-okkal (név, tárolási típus, `RawValueKind`, null értékűség, kulcs, `IsSensitive` a `[Sensible]` alapján). A `RawTable.EntityName` az audit entitásnév (`IReadOnlyEntityType.AuditEntityName`, az EF audittal közös).
- A `RawDatabaseExtensions` paraméterezett SQL-lel olvassa, számolja, szúrja be, módosítja és törli a sorokat. A tábla- és oszlopnevek kizárólag a modellből származnak, az értékeket az oszlopuk fajtája szerint értelmezi (`RawColumn.Parse`), az olvasott értékeket pedig normalizálja (`Normalize`: Guid szöveg, Base64 bináris, logikai értékek) és maszkolja (`Present`, `RawTable.Mask`).
- A `RawDatabaseCommands` (Core) tranzakción belül ír, és az audit bejegyzést maga rögzíti (`DbContextAuditExtensions.AddAuditEntry`), mert a nyers SQL megkerüli a változáskövetőt: `Created`, `Updated` (a sor előtte és utána különbségei) és `HardDeleted`. A `RawDatabasePolicyExtensions` felsorolja a csak olvasható entitásokat (`AuditLogEntry`).
- A soft delete nem érvényesül: a nyers olvasások minden sort látnak, a nyers törlés pedig véglegesen eltávolítja a sort.
- A `RawDatabaseCatalogTests` elbukik, ha a modell egy entitástípusát nem fedi le a szerkesztő; így ellenőrizhető egy új entitás (lásd az `add-raw-database-editor` skillt).

## Modulok és az összevont adatbázis

A Behind a kötelező Core modul mellett több modult is képes hostolni. Egy modul sosem hivatkozik másik modulra (a Core-ra sem): csak a `Behind.Common*` projektekre hivatkozik, és egy `ModuleDescriptor`-ral (`Behind.Common.Modules`) írja le magát, benne a `ModuleDatabase`-ével:

- `ConfigurationAssembly`: a modul `IEntityTypeConfiguration<T>` osztályait tartalmazó assembly.
- `SoftDeletedEntities`: a gyökérentitások, amelyek megkapják a soft delete lekérdezési szűrőt.
- `ReadOnlyEntities`: az entitásnevek, amelyeket a nyers adatbázis-szerkesztő nem írhat.

Az adatbázis a projektben összevont: szolgáltatónként egy adatbázis és egy DbContext. A projekt kontextusa az `AbstractCoreContext`-ből (a Core kontextus, amely kötelező és mindig betöltődik) származik, megvalósítja minden modul kontextusfelületét (az `INotesContext` a `DbSet`-jeivel), és a konstruktorában megkapja a `ModuleCatalog`-ot (`AbstractCoreContext(options, modules)`), így az `OnModelCreating` alkalmazza minden modul entitáskonfigurációit és soft delete-jét. Egy modulkontextus csak felületként érhető el: a modul az `services.AddModuleContext<INotesContext>()` hívást regisztrálja, amely a projekt kontextusát az `IUnitOfWork`-ön keresztül oldja fel, és azonnal hibával leáll, ha a projekt kontextusa nem valósítja meg a felületet.

Regisztráció (a Core alapértelmezései a `RegisterSqlite(builder)`/`RegisterPostgres(builder)` maradnak):

```csharp
database.RegisterSqlite<WebContextSqlite>(builder);
database.RegisterPostgres<WebContextPostgres>(builder);
```

a projekt kontextusát `AbstractCoreContext`/`IUnitOfWork` néven regisztrálja, a Core migrátort a sima `CoreContextSqlite`/`CoreContextPostgres`-en keresztül működőképesen tartja, és regisztrálja az `AccessCatalog`-ot (Core plusz a modulok jogosultságai).

### Modulok migrációi

A Core saját migrációs lánccal rendelkezik (`CoreContextSqlite`, előzménytábla: `__EFMigrationsHistory`). Minden modulnak is saját migrációs lánca van, saját előzménytáblával (`ModuleDescriptor.MigrationsHistoryTable`, `__<key>MigrationsHistory`), így a láncok függetlenek, és egy modul a Core migrációinak érintése nélkül adható egy projekthez vagy távolítható el belőle:

1. A modul szolgáltatóprojektje (`Behind.<Module>.Database.Sqlite`/`.Postgres`) tartalmaz egy csak tervezési időben használt kontextust (`<Module>ContextSqlite : DbContext`), amely csak a modul modelljét alkalmazza (`modelBuilder.ApplyModuleConfigurations(...)`), a migrációkat és egy tervezési idejű factoryt, amely a `module.MigrationsHistoryTable`-t használja.
2. A projekt migrátorprojektje regisztrálja: `database.RegisterSqliteModule<BlogContextSqlite>(builder, BlogModule.Descriptor)` (PostgreSQL esetén `RegisterPostgresModule`). Ez modulonként egy `IModuleMigrator`-t (`ContextModuleMigrator<TContext>`) regisztrál.
3. A `MigratorService` alkalmazza a Core migrációkat, majd minden `IModuleMigrator`-t, azután a verziózási migrációkat, végül a kezdeti adatokat: a beépített felhasználókat, a beépített szerepköröket (az `EnsureBuiltInRolesAsync(accessCatalog)` hozzáadja a modulok jogosultságait a beépített szerepkörökhöz, és megtartja az adminisztrátorok szerkesztéseit), a beépített menüket (az `EnsureBuiltInMenusAsync(moduleCatalog)` hozzáadja a modulok alapértelmezett menüelemeit) és a modulok minden `IModuleSeed`-jét a saját alapértelmezett adataikhoz.

A modultáblák sosem rendelkeznek idegen kulccsal a Core táblákra; a felhasználókra és más Core rekordokra csak azonosító alapján hivatkoznak.

### Modulok audit címkéi és nyers adatbázis-szerkesztője

- Egy modul regisztrál egy `IAuditLabelSource`-t (`LabelsAsync(ids)`, amely `AuditLabel(id, label)` értékeket ad vissza), hogy az auditnapló ember számára olvasható neveket mutasson az entitásaihoz; az `AuditLogEntryQueries` minden forrás címkéit összefésüli a Core címkéivel.
- Az összevont modell minden táblája automatikusan szerkeszthető a nyers adatbázis-szerkesztőben. Az érzékeny tulajdonságokat jelölje meg `[Sensible]` attribútummal, a csak olvasható táblákat pedig vegye fel a `ModuleDatabase.ReadOnlyEntities` listába.

### A Quotes példamodul

A `Quote` a `Quotes` táblában tárolódik (`Key`, `Version`, `Text`, `Author`; összetett elsődleges kulcs: `Key` + `Version`, index az `Author` oszlopon). A mentés a következő verziót szúrja be, és sosem módosít sort; a létrehozás és módosítás ideje az auditnaplóból származik, így a táblának nincsenek időbélyeg-oszlopai, és nincsenek idegen kulcsai. A `Quote` szerepel a `ModuleDatabase.SoftDeletedEntities` listában, így a törlés soft delete (minden verzióra), a fejlesztők listázhatják a törölt idézeteket, visszaállíthatják őket és véglegesen törölhetik őket.

A modul parancsai és lekérdezései az `IQuotesContext` modulkontextus-felületet használják, amely az `IModuleContext`-et (`Behind.Common.Database/Modules`) bővíti: az `IModuleContext` egy `ISoftDeleteContext`, amely kiadja az alatta lévő `DbContext`-et (`Context`), és bármely lekérdezést szűr a `DeletionState` szerint (`InDeletionState`). Az `AbstractCoreContext` megvalósítja, így a `WebContextSqlite`/`WebContextPostgres` projektkontextus csak a `DbSet<Quote> Quotes`-t adja hozzá az `IQuotesContext` teljesítéséhez, a modul pedig sosem hivatkozik a Core-ra. A modul migrációs előzménytáblája a `__quotesMigrationsHistory`; a migrációk a `Behind.Quotes.Database.Sqlite/Migrations` és a `Behind.Quotes.Database.Postgres/Migrations` mappában találhatók, és a `Behind/` mappából hozhatók létre:

```bash
dotnet ef migrations add "<Name>" --project src/Quotes/Behind.Quotes.Database.Sqlite --startup-project src/Web/Behind.Web.Database.Migrator --context QuotesContextSqlite --output-dir Migrations
dotnet ef migrations add "<Name>" --project src/Quotes/Behind.Quotes.Database.Postgres --startup-project src/Web/Behind.Web.Database.Migrator --context QuotesContextPostgres --output-dir Migrations
```

A `QuotesAuditLabelSource` az auditnaplóban egy idézet szövegét mutatja (a legújabb verzió, a töröltek is), a `QuotesSeed` egyszer hozzáad egy üdvözlő idézetet, a `Quotes` tábla pedig szerkeszthető a nyers adatbázis-szerkesztőben, mint bármely más tábla.

## Migrációk

### Migrációk létrehozása

```bash
cd Behind

# Új migráció létrehozása (core kontextus)
dotnet ef migrations add "AddUserTable" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite \
    --output-dir Migrations

# Új migráció létrehozása (verziózási kontextus)
dotnet ef migrations add "AddSomething" \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context VersioningContextSqlite \
    --output-dir Migrations/Versioning

# Az utolsó migráció eltávolítása
dotnet ef migrations remove \
    --project src/Core/Behind.Core.Database.Sqlite \
    --startup-project src/Core/Behind.Core.Database.Migrator \
    --context CoreContextSqlite
```

A migrációk a `Behind.Core.Database.Sqlite` projektben találhatók; a `Behind.Core.Database.Migrator` futtatásával alkalmazhatók.

PostgreSQL esetén használja ugyanezeket a parancsokat a `--project src/Core/Behind.Core.Database.Postgres` és a `--context CoreContextPostgres` (kimenet: `Migrations`), illetve a `--context VersioningContextPostgres` (kimenet: `Migrations/Versioning`) beállítással. A kontextusoknak vannak tervezési idejű factoryjaik, így az indítóprojektnek csak a `Microsoft.EntityFrameworkCore.Design` csomagra kell hivatkoznia. Tartsa szinkronban a két szolgáltatót: egy modellváltozáshoz mindkét szolgáltatóprojektben kell migráció (ellenkező esetben a `PostgresMigrations_MatchTheModelSnapshots` elbukik).

### Migrációs ajánlott gyakorlatok

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

**Irányelvek**:
- Migrációnként egy logikai változás
- Beszédes migrációnevek
- Visszafordítható Down() metódus
- Indexek a lekérdezhető oszlopokhoz
- Megszorítások az adatintegritás érdekében

## Tranzakciókezelés

### Explicit tranzakciók

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

### SavePoint-támogatás

```csharp
public async Task<Exceptional<User>> RegisterWithAuditAsync(
    RegisterDto dto,
    CancellationToken cancellationToken = default)
{
    await using (var transaction = await dbContext.Database.BeginTransactionAsync())
    {
        try
        {
            // Felhasználó létrehozása
            var user = new HumanUser(dto.DisplayName, new[] { new Email(dto.Email) }, None);
            await users.AddAsync(user, cancellationToken);

            // Savepoint az audit előtt
            await transaction.CreateSavepointAsync("BeforeAudit", cancellationToken);

            // Audit létrehozása (hibázhat)
            var auditLog = CreateAuditLog(user);
            await auditLogs.AddAsync(auditLog, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return user;
        }
        catch (Exception ex) when (ex is AuditException)
        {
            // Visszaállás a savepointra, a felhasználó megtartása
            await transaction.RollbackToSavepointAsync("BeforeAudit", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return user;  // A felhasználó audit nélkül jött létre
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ex;
        }
    }
}
```

## Teljesítményszempontok

### N+1 lekérdezési probléma

**Rossz** ✗
```csharp
var users = dbSet.ToList();
foreach (var user in users)
{
    var roles = dbSet.Entry(user)
        .Collection(u => u.Roles)
        .Query()
        .ToList();  // Felhasználónként külön lekérdezés!
}
```

**Jó** ✓
```csharp
var users = await dbSet
    .Include(u => u.Roles)
    .ThenInclude(r => r.Permissions)
    .ToListAsync();  // Egyetlen lekérdezés joinokkal
```

### Lekérdezési projekció

**Rossz** ✗
```csharp
var users = await dbSet.ToListAsync();  // Teljes entitások betöltése
var userDtos = users.Select(MapToDto).ToList();  // Leképezés memóriában
```

**Jó** ✓
```csharp
var userDtos = await dbSet
    .Select(u => new UserDto
    {
        Id = u.Id,
        Email = u.Email.Value,
        DisplayName = (u as HumanUser)!.DisplayName
    })
    .ToListAsync();  // A projekció az adatbázisba tolódik
```

### Indexelési stratégia

```csharp
// Elsődleges keresések
builder.HasIndex(u => u.Id).IsUnique();

// Idegen kulcsok
builder.HasIndex(u => u.RoleId);

// Szűrés
builder.HasIndex(u => u.CreatedAt);
builder.HasIndex(u => u.IsActive);

// Összetett indexek
builder.HasIndex(u => new { u.IsActive, u.CreatedAt });
```

## Adatbázis-kapcsolat

### Kapcsolati karakterláncok

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db",  // SQLite
    "PostgreSQL": "Host=localhost;Port=5432;Database=behind;User=postgres;******"
  }
}
```

### DbContext regisztrációja

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
                .EnableSensitiveDataLogging()  // Csak fejlesztéshez
                .LogTo(Console.WriteLine)      // Csak fejlesztéshez
        );

        services.AddScoped<IUnitOfWork, ApplicationUnitOfWork>();
        services.AddScoped<IRepository<User>, UserRepository>();
        services.AddScoped<IRepository<Role>, RoleRepository>();

        return services;
    }
}
```

## Összefoglalás

| Minta | Cél | Helye |
|---------|---------|----------|
| DbContext | ORM-koordináció | Behind.Core.Database |
| IEntityTypeConfiguration | Fluent leképezések | Behind.Core.Database |
| IRepository<T> | Adatelérési absztrakció | Behind.Common.Database |
| IUnitOfWork | Tranzakciókoordináció | Behind.Core.Database |
| Filter<T> | Lekérdezési specifikációk | Behind.Common |
| Migrations | Séma verziózása | Behind.Core.Database.Migrator |

---

**Legfontosabb tanulságok**:
- Használjon repository mintát az adatelérés absztrakciójához
- Használja a Unit of Work mintát a több repositoryt érintő tranzakciókhoz
- Használjon szűrőket/specifikációkat az újrafelhasználható lekérdezési logikához
- Konfigurálja az entitásokat a Fluent API-val
- Kezelje gondosan a migrációkat
- Optimalizálja a lekérdezéseket az N+1 problémák elkerülése érdekében
