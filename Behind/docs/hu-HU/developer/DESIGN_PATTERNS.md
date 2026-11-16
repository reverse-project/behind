# Tervezési minták és architekturális döntések

Ez a dokumentum a Behind kódbázis architekturális mintáit, tervezési döntéseit és azok indoklását ismerteti.

## Rétegzett architektúra

A Behind **tiszta rétegzett architektúrát** követ, a felelősségi körök egyértelmű szétválasztásával:

```
┌─────────────────────────────────┐
│   Web/API réteg                 │ (Behind.Web, Behind.Core.WebApi)
│   - Controllerek                │ - HTTP végpontok
│   - Kérés/válasz DTO-k          │ - Útvonalkezelők
│   - API middleware              │ - CORS, hitelesítés
├─────────────────────────────────┤
│   Domain réteg                  │ (Behind.Core.Domain)
│   - Domain modellek (record)    │ - Üzleti szabályok
│   - Aggregátumok                │ - Értékobjektumok
│   - Domain események            │ - Domain logika
├─────────────────────────────────┤
│   Alkalmazási réteg             │ (Behind.Core.Database)
│   - DbContext                   │ - Felhasználási esetek
│   - Entitáskonfigurációk        │ - Alkalmazáslogika
│   - Repository implementációk   │ - Adatbetöltés
├─────────────────────────────────┤
│   Infrastruktúra réteg          │ (Behind.Common.*)
│   - Adatbázis-absztrakciók      │ - Általános minták
│   - API segédeszközök           │ - Konfiguráció
│   - Funkcionális segédeszközök  │ - Szerializáció
└─────────────────────────────────┘
```

### Rétegek felelősségei

#### 1. Web/API réteg (Behind.Core.WebApi, Behind.Web)
**Felelősségek**:
- HTTP kérések/válaszok kezelése
- A felhasználói bemenet ellenőrzése (alapvető szintaktikai ellenőrzések)
- Alkalmazásszolgáltatások meghívása
- A válaszok formázása a kliensek számára
- Hitelesítés/jogosultságkezelés megvalósítása

**Szabályok**:
- Nincs üzleti logika
- Nincsenek közvetlen adatbázis-lekérdezések
- A domain és az alkalmazási rétegtől függ
- Bemenetként/kimenetként mindig DTO-kat kell használni

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService service;
    
    [HttpPost]
    public async Task<IActionResult> CreateUserAsync(CreateUserDto dto)
    {
        var result = await service.CreateUserAsync(dto);
        
        return result.Match(
            errors => BadRequest(new { errors }),
            user => CreatedAtAction(nameof(GetUserAsync), new { id = user.Id }, user)
        );
    }
}
```

#### 2. Domain réteg (Behind.Core.Domain)
**Felelősségek**:
- Domain modellek és aggregátumok
- Üzleti szabályok és validáció
- Domain események
- Értékobjektumok

**Szabályok**:
- Független az infrastruktúrától
- Nem ismeri az adatbázist
- Az üzleti logikát érthetően fejezi ki
- Megváltoztathatatlan (immutable) record-ok

```csharp
public abstract record User(Option<IEnumerable<Role>> Roles)
{
    public bool HasPermission(Grant grant) =>
        Roles.Match(
            () => false,
            roles => roles.Any(r => r.HasPermission(grant))
        );
}

public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }
    public string DisplayName { get; init; }
    
    public Validation<HumanUser> UpdateEmail(Email newEmail) =>
        Emails.Contains(newEmail)
            ? Invalid(new Error("EMAIL_DUPLICATE", "Email already exists"))
            : Valid(this with { Emails = Emails.Append(newEmail) });
}
```

#### 3. Alkalmazási réteg (Behind.Core.Database)
**Felelősségek**:
- DbContext konfigurálása
- Entitáskonfigurációk
- Repository implementációk
- Felhasználási esetek összehangolása
- Adatbetöltés és perzisztálás

**Szabályok**:
- A domain interfészeit valósítja meg
- Entity Framework Core-t használ
- Ismeri a perzisztencia mechanizmusait
- Az adatműveleteket hangolja össze

```csharp
public class UserRepository : IRepository<User>
{
    private readonly DbSet<User> dbSet;
    
    public async Task<Option<User>> TryFindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbSet
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        
        return user;
    }
    
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await dbSet.AddAsync(user, cancellationToken);
    }
}
```

#### 4. Infrastruktúra réteg (Behind.Common.*)
**Felelősségek**:
- Általános, újrafelhasználható absztrakciók
- Szerializáció/deszerializáció
- Konfigurációkezelés
- Naplózás és megfigyelhetőség beállítása
- Keretrendszeri segédeszközök

**Szabályok**:
- Általánosnak és újrafelhasználhatónak kell lennie
- Nincs domain-specifikus logika
- Nincs üzleti logika
- Egyetlen felelősség

```csharp
// Általános repository interfész (domain ismeret nélkül)
public interface IRepository<TAggregate, TKey>
{
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}

// Általános szűrőkifejezés
public record Filter<T>(Expression<Func<T, bool>> Predicate)
{
    public IQueryable<T> Apply(IQueryable<T> queryable) => queryable.Where(Predicate);
}

// Általános JSON konverter
public class OptionJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType &&
           typeToConvert.GetGenericTypeDefinition() == typeof(Option<>);
}
```

## Tervezési minták

### 1. Repository minta

**Cél**: A domain leválasztása az adatelérésről; adatelérési absztrakció biztosítása.

**Felépítés**:
```csharp
// Interfész a Behind.Common.Database projektben (általános)
public interface IRepository<TAggregate, TKey>
{
    TKey GetKey(TAggregate aggregate);
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    void Update(TAggregate aggregate);
    void Delete(TAggregate aggregate);
}

public interface IRepository<T> : IRepository<T, Guid> where T : class;

// Implementáció a Behind.Core.Database projektben (domain-specifikus)
public class UserRepository : IRepository<User>
{
    private readonly DbSet<User> dbSet;
    private readonly DbContext dbContext;
    
    public Guid GetKey(User user) => user.Id;
    
    public async Task<Option<User>> TryFindAsync(Guid key, CancellationToken cancellationToken)
    {
        return await dbSet.FirstOrDefaultAsync(u => u.Id == key, cancellationToken);
    }
    
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await dbSet.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

**Előnyök**:
- Tesztelhető (a repository könnyen mockolható)
- Cserélhető adatforrások
- Központosított lekérdezési logika
- Egységes adatelérési minták

### 2. Unit of Work minta

**Cél**: Több repository összehangolása egyetlen tranzakcióként.

**Interfész**:
```csharp
public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<User> Users { get; }
    IRepository<Role> Roles { get; }
    IRepository<Session> Sessions { get; }
    
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

**Használat**:
```csharp
public async Task<Exceptional<User>> RegisterUserAsync(RegisterDto dto)
{
    var user = new User(dto);
    var defaultRole = await uow.Roles.TryFindAsync(Guid.Parse(DomainConstants.DefaultRoleId));
    
    user.AssignRole(defaultRole);
    
    await uow.Users.AddAsync(user);
    await uow.CommitAsync();  // Minden repository együtt véglegesít
    
    return user;
}
```

**Előnyök**:
- ACID tranzakciók
- Konzisztencia a repository-k között
- Visszagörgetési lehetőség
- A felelősségi körök tiszta szétválasztása

### 3. Aggregátum minta (Domain-Driven Design)

**Cél**: Az összetartozó entitások és értékobjektumok összefüggő aggregátumokba csoportosítása, egyértelmű határokkal.

**Meghatározás**: Az aggregátum egymással összefüggő objektumok csoportja, amelyet az adatkonzisztencia szempontjából egyetlen egységként kezelünk.

**Felépítés**:
```csharp
// Aggregátum gyökér
public abstract record User(Option<IEnumerable<Role>> Roles)
{
    public Guid Id { get; init; }
    
    // Üzleti műveletek az aggregátumon
    public Validation<User> AddRole(Role role) 
    {
        var newRoles = (Roles.AsEnumerable() ?? []).Append(role);
        return Valid(this with { Roles = Some(newRoles) });
    }
    
    public bool HasPermission(Grant grant) =>
        Roles.Match(
            () => false,
            roles => roles.Any(r => r.HasPermission(grant))
        );
}

// Az aggregátumhoz tartozó gyermek entitások
public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }  // Értékobjektumok
    public string DisplayName { get; init; }
}

// Értékobjektumok
public record Email(string Value);
public record Role(string Name, Option<IEnumerable<Grant>> Grants);
public record Grant(string Name);
```

**Szabályok**:
- Hozzáférés kizárólag az aggregátum gyökerén keresztül
- Aggregátumonként egyetlen felelősség
- Az aggregátumon belül megváltoztathatatlan
- Tranzakciós konzisztenciahatár

**Példa - Aggregátum a repository-ban**:
```csharp
public class UserRepository : IRepository<User>
{
    public async Task<Option<User>> TryFindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbSet
            .Include(u => u.Roles)        // A kapcsolódó Role entitások betöltése
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        
        return user;  // A teljes aggregátum vagy None visszaadása
    }
    
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await dbSet.AddAsync(user, cancellationToken);
        // Az összes kapcsolódó entitás automatikusan hozzáadódik
    }
}
```

### 4. Specifikáció minta

**Cél**: A lekérdezési logika kapszulázása újrafelhasználható, tesztelhető objektumokba.

**Megvalósítás szűrőkkel**:
```csharp
// Általános szűrő
public record Filter<T>(Expression<Func<T, bool>> Predicate)
{
    public IQueryable<T> Apply(IQueryable<T> queryable) => queryable.Where(Predicate);
}

// Konkrét specifikációk
public static class UserSpecifications
{
    public static Filter<User> ByEmail(Email email) =>
        new(u => u.Emails.Contains(email));
    
    public static Filter<User> Active =>
        new(u => !(u is BannedUser));
    
    public static Filter<User> WithRole(Role role) =>
        new(u => u.Roles.Match(
            () => false,
            roles => roles.Contains(role)
        ));
}

// Használat
var activeUsersWithAdminRole = await dbSet
    .Where(UserSpecifications.Active.Predicate)
    .Where(UserSpecifications.WithRole(adminRole).Predicate)
    .ToListAsync();
```

**Előnyök**:
- Újrafelhasználható lekérdezési logika
- A repository-któl külön tesztelhető
- Olvashatóbb lekérdezések
- A gyakori lekérdezések egyetlen helyen frissíthetők

### 5. Függőséginjektálás

**Cél**: A függőségek szétválasztása és a futásidejű összeállíthatóság biztosítása.

**Konfiguráció** (a Behind.Common.WebApi projektben):
```csharp
extension(IServiceCollection)
{
    public IServiceCollection AddUserServices(this IServiceCollection services)
    {
        services.AddScoped<IRepository<User>, UserRepository>();
        services.AddScoped<IRepository<Role>, RoleRepository>();
        services.AddScoped<IUnitOfWork, ApplicationUnitOfWork>();
        services.AddScoped<UserService>();
        services.AddScoped<AuthenticationService>();
        
        return services;
    }
}
```

**Használat** (a controllerben):
```csharp
[ApiController]
public class AuthController
{
    private readonly IUnitOfWork unitOfWork;
    private readonly AuthenticationService authService;
    
    public AuthController(IUnitOfWork unitOfWork, AuthenticationService authService)
    {
        this.unitOfWork = unitOfWork;
        this.authService = authService;
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LoginDto dto)
    {
        var result = await authService.AuthenticateAsync(dto.Email, dto.Password);
        return result.Match(
            ex => Unauthorized(),
            token => Ok(token)
        );
    }
}
```

**Előnyök**:
- Laza csatolás
- Hamis (fake) implementációkkal tesztelhető
- Indításkor konfigurálható
- Követi a függőségek megfordításának elvét (Dependency Inversion)

### 6. Decorator minta

**Cél**: Viselkedés dinamikus hozzáadása az objektumokhoz a felépítésük módosítása nélkül.

**Példa** (Repository naplózással):
```csharp
public class LoggingUserRepository : IRepository<User>
{
    private readonly IRepository<User> inner;
    private readonly ILogger<LoggingUserRepository> logger;
    
    public async Task<Option<User>> TryFindAsync(Guid key, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Finding user {key}");
        var result = await inner.TryFindAsync(key, cancellationToken);
        
        result.Match(
            () => logger.LogWarning($"User {key} not found"),
            user => logger.LogInformation($"Found user {user.Id}")
        );
        
        return result;
    }
    
    // ... további metódusok
}

// Regisztráció
services.AddScoped<IRepository<User>, UserRepository>();
services.Decorate<IRepository<User>, LoggingUserRepository>();
```

### 7. Factory minta

**Cél**: Az összetett objektumlétrehozási logika kapszulázása.

**JSON Converter Factory**:
```csharp
public class OptionJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType &&
           typeToConvert.GetGenericTypeDefinition() == typeof(Option<>);
    
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
```

**Használat**:
```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new OptionJsonConverterFactory());
options.Converters.Add(new ExceptionalJsonConverterFactory());

var json = JsonSerializer.Serialize(userWithOptionalFields, options);
```

## Architekturális minták

### 1. Vasútorientált programozás (Railway-Oriented Programming, ROP)

A FUNCTIONAL_PROGRAMMING.md dokumentum már részletesen bemutatta. A hibakezelésre mindenhol ezt használjuk.

```csharp
public async Task<Exceptional<User>> AuthenticateAsync(Credentials creds)
{
    return FindUser(creds.Email)
        .Map(user => ValidatePassword(user, creds.Password))
        .Map(GenerateSessionToken)
        .Bind(SaveSession);
}
```

### 2. CQRS (Command Query Responsibility Segregation)

**Az olvasási és írási modellek szétválasztása**:

```csharp
// Parancs (írási művelet)
public record CreateUserCommand(Email Email, string DisplayName);

public class CreateUserCommandHandler
{
    public async Task<Exceptional<Guid>> HandleAsync(CreateUserCommand cmd)
    {
        var user = new HumanUser(cmd.DisplayName, new[] { cmd.Email }, None);
        await repository.AddAsync(user);
        return user.Id;
    }
}

// Lekérdezés (olvasási művelet)
public record GetUserQuery(Guid UserId);

public class GetUserQueryHandler
{
    public async Task<Exceptional<UserDto>> HandleAsync(GetUserQuery query)
    {
        var user = await repository.TryFindAsync(query.UserId);
        return user.Match(
            () => new InvalidOperationException("User not found"),
            u => MapToDto(u)
        );
    }
}
```

### 3. Service Locator (Anti-minta - kerülendő)

**Ne használja**:
```csharp
// ❌ ROSSZ: Rejtett függőségeket hoz létre
public class UserService
{
    public async Task<User> GetUserAsync(Guid id)
    {
        var repository = ServiceLocator.GetService<IRepository<User>>();
        return await repository.FindAsync(id);
    }
}
```

**Használjon helyette függőséginjektálást**:
```csharp
// ✓ JÓ: Explicit függőségek
public class UserService
{
    private readonly IRepository<User> repository;
    
    public UserService(IRepository<User> repository)
    {
        this.repository = repository;
    }
    
    public async Task<Option<User>> GetUserAsync(Guid id)
    {
        return await repository.TryFindAsync(id);
    }
}
```

## Adatfolyam

### A teljes kérés útja

```
HTTP kérés
    ↓
[API Controller/Végpont]
    ↓ (A bemenet szintaxisának ellenőrzése)
[Alkalmazásszolgáltatás]
    ↓ (Az üzleti szabályok ellenőrzése)
[Domain logika/Aggregátumok]
    ↓ (Az üzleti művelet végrehajtása)
[Repository/Adatelérés]
    ↓ (Mentés az adatbázisba)
[Entity Framework Core]
    ↓
[Adatbázis]
    ↓ (Az eredmény visszaadása)
[Exceptional<T> / Validation<T>]
    ↓ (Leképezés válasz DTO-ra)
[API válasz]
```

### Példa: Felhasználó regisztrációja

```csharp
// 1. Megérkezik a HTTP kérés
[HttpPost("register")]
public async Task<IActionResult> RegisterAsync(RegisterDto dto)
{
    // 2. A controller ellenőrzi a szintaxist, és meghívja a szolgáltatást
    var result = await userService.RegisterAsync(dto);
    
    // 3. A szolgáltatás ellenőrzi az üzleti szabályokat
    // 4. A domain létrehozza az aggregátumot
    // 5. A repository elmenti
    
    return result.Match(
        errors => BadRequest(new { errors }),
        user => CreatedAtAction(nameof(GetUser), new { id = user.Id })
    );
}

// Alkalmazásszolgáltatás
public class UserService
{
    public async Task<Validation<User>> RegisterAsync(RegisterDto dto)
    {
        // Ellenőrzés: az e-mail-cím egyedi
        var existingOption = await repository.TryFindByEmailAsync(new Email(dto.Email));
        
        return from _ in existingOption.Match(
                () => Valid(Unit.Default),
                _ => Invalid(new Error("EMAIL_EXISTS", "Email already registered"))
            )
        from validEmail in ValidateEmail(dto.Email)
        from validPassword in ValidatePassword(dto.Password)
        select await CreateUser(validEmail, validPassword);
    }
    
    private async Task<User> CreateUser(Email email, string passwordHash)
    {
        var user = new HumanUser(
            displayName: email.Value.Split('@')[0],
            emails: new[] { email },
            roles: None
        );
        
        await repository.AddAsync(user);
        return user;
    }
}
```

## Alkalmazott tervezési elvek

### DRY (Don't Repeat Yourself – ne ismételje önmagát)
- Közös kód a Behind.Common projektben
- Alap repository a Behind.Common.Database projektben
- Projektek között megosztott kiterjesztések

### KISS (Keep It Simple, Stupid – legyen egyszerű)
- Kis, jól körülhatárolt függvények
- Átlátható adatfolyam
- Minimális absztrakciók

### SOLID

1. **Single Responsibility (egyetlen felelősség)** - Minden osztálynak egyetlen oka van a változásra
2. **Open/Closed (nyitott/zárt)** - Interfészeken keresztül bővítjük, nem módosítással
3. **Liskov Substitution (Liskov-féle helyettesítés)** - Az altípusok helyettesíthetik az ősüket
4. **Interface Segregation (interfészek szétválasztása)** - Az ügyfelek nem függenek nem használt metódusoktól
5. **Dependency Inversion (függőségek megfordítása)** - Absztrakcióktól függünk, nem konkrét megvalósításoktól

### Clean Code
- Önmagát dokumentáló kód
- Beszédes nevek
- Nincs szükség megjegyzésekre
- Kis függvények

## Döntési mátrix

| Forgatókönyv | Minta | Példa |
|--------------|-------|-------|
| Opcionális érték keresése | Option<T> + Repository | `TryFindAsync(id)` |
| Kivételek kezelése | Try<T> vagy Exceptional<T> | `ParseJson<T>(json)` |
| Bemenet ellenőrzése | Validation<T> | `ValidateRegistration(dto)` |
| Műveletek összefűzése | Bind/SelectMany | Monádműveletek láncolása |
| Lekérdezések kapszulázása | Filter/Specification | UserSpecifications.ByEmail |
| Konzisztencia kezelése | Aggregátum | User aggregátum Roles-szal |
| Repository-k összehangolása | Unit of Work | Több repository-t érintő tranzakciók |

---

**Következő lépések**:
- Tekintse át a tényleges megvalósításokat a kódbázisban
- Az adatréteg részleteiért lásd a DATABASE_ARCHITECTURE.md fájlt
- Az API-specifikus mintákért lásd a WEBAPI_CONVENTIONS.md fájlt

