# Kódolási szabványok és konvenciók

Ez a dokumentum azokat a kötelező kódolási szabványokat ismerteti, amelyeket minden fejlesztőnek követnie kell. Ezek a szabványok biztosítják az egységességet, a karbantarthatóságot és a kódminőséget a teljes Behind kódbázisban.

## 🚫 Aranyszabályok

### 1. NINCS MEGJEGYZÉS A KÓDBAN
**Szabály**: Ha a kódhoz megjegyzés kell, akkor rossz a kód. A kódnak önmagát kell dokumentálnia.

**Mit jelent ez**:
- Használjon világos, leíró neveket a változókhoz, metódusokhoz és osztályokhoz
- Írjon kicsi, egy feladatra összpontosító függvényeket, egyetlen felelősséggel
- Használjon erős típusosságot a szándék kifejezésére
- Szervezze a kódot logikusan, értelmes szerkezetben

**Rossz példa** ✗
```csharp
public class UserService
{
    // Ellenőrzi, hogy létezik-e a felhasználó az adatbázisban
    public async Task<bool> CheckUser(string id)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        return u != null; // A felhasználó megvan
    }
}
```

**Jó példa** ✓
```csharp
public class UserRepository
{
    public async Task<Option<User>> TryFindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbSet.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user;
    }
}
```

### 1a. NINCS KAPCSOS ZÁRÓJEL NÉLKÜLI VEZÉRLÉSI UTASÍTÁS
**Szabály**: Soha ne használjon rövidített `if`, `else`, `for`, `foreach`, `while` vagy `using` utasításokat kapcsos zárójel nélkül. Mindig kapcsos zárójeles blokkot használjon, a törzzsel a saját soraiban. Ez megelőzi az olyan hibákat, mint az Apple „goto fail” esete.

**Rossz példa** ✗
```csharp
if (session is null) return None;
```

**Jó példa** ✓
```csharp
if (session is null)
{
    return None;
}
```

### 2. BŐVÍTŐ METÓDUSOK C# 12 SZINTAXISSAL
**Szabály**: Minden bővítő metódushoz az új `extension(Type)` C# 12 szolgáltatást használja.

**Fájlelnevezés**: Minden bővítőfájl nevének `Extensions` végződésűnek kell lennie (pl. `StringExtensions.cs`, `DbContextExtensions.cs`)

**Szintaxis**:
```csharp
using System.Collections.Immutable;

namespace Behind.Common;

extension(IQueryable<T>)
{
    public IQueryable<T> Where(Filter<T> filter)
    {
        return filter.Apply(this);
    }
}

extension(IConfiguration)
{
    public JsonNode Serialize()
    {
        var children = this.GetChildren().ToList();
        // megvalósítás
        return jsonObject;
    }
}
```

**Miért**: A modern C# 12 szintaxis tisztább, kifejezőbb, és egyértelműen jelöli a bővítéseket.

### 3. REKORDOK, VÁLTOZTATHATATLAN OBJEKTUMOK ÉS ASYNC/AWAIT HASZNÁLATA
**Szabály**: Minden I/O-művelethez és adatszerkezethez részesítse előnyben a változtathatatlanságot és az aszinkron mintákat.

**Rekordok használata**:
```csharp
public record Email(string Value);
public record Permission(string Name, Option<IEnumerable<Grant>> Grants);
public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }
    public string DisplayName { get; init; }
}
```

**A változtathatatlanság előnyei**:
- Alapból szálbiztos
- Hibák egész osztályait szünteti meg
- Egyértelmű értékszemantika
- Beépített egyenlőségvizsgálat

**Async/await mindenhol**:
```csharp
public async Task<Option<User>> TryFindAsync(Guid id, CancellationToken cancellationToken = default)
{
    var user = await dbSet.FirstOrDefaultAsync(
        u => u.Id == id, 
        cancellationToken
    );
    return user;
}

public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
{
    dbSet.Update(user);
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

### 4. FUNKCIONÁLIS ESZKÖZÖK ÉS MONÁDOK HASZNÁLATA
**Szabály**: A vezérlési folyamathoz null értékek és kivételek helyett funkcionális programozási szerkezeteket (Option, Try, Exceptional, Validation) használjon.

**Elérhető monádok**:

| Monád | Cél | Példa |
|-------|---------|---------|
| `Option<T>` | A null értékek helyettesítője | `Option<User> user` |
| `Try<T>` | Kivételek becsomagolása | `Try<Auth> result` |
| `Exceptional<T>` | Kivétel vagy siker | `public Exceptional<Auth> Authenticate(...)` |
| `Validation<T>` | Hibák gyűjtése | `Validation<Command> ValidateCommand(...)` |

**Példák**:

```csharp
public Option<User> FindUser(Guid id)
{
    return dbSet.FirstOrDefault(u => u.Id == id);
}

public async Task<Exceptional<AuthToken>> AuthenticateAsync(Credentials creds)
{
    try
    {
        var user = await FindUserAsync(creds.Email);
        return user.Match(
            () => new ArgumentException("User not found") as Exceptional<AuthToken>,
            u => ValidatePassword(u, creds.Password)
                .Match(
                    ex => ex as Exceptional<AuthToken>,
                    valid => GenerateToken(u) as Exceptional<AuthToken>
                )
        );
    }
    catch (Exception ex)
    {
        return ex;
    }
}

public Validation<UpdateUserCommand> ValidateUpdate(UpdateUserDto dto)
{
    return (
        from displayName in ValidateDisplayName(dto.DisplayName)
        from emails in ValidateEmails(dto.Emails)
        select new UpdateUserCommand(displayName, emails)
    );
}
```

### 5. MEGVALÓSÍTÁS A MEGFELELŐ RÉTEGEKBEN
**Szabály**: A közös kódot a megfelelő megosztott könyvtárba helyezze.

**Rétegekre vonatkozó irányelvek**:

| Réteg | Cél | Példák |
|-------|---------|----------|
| `Behind.Common` | Újrafelhasználható absztrakciók, segédeszközök | Option, Try, Validation, funkcionális bővítmények, szűrők |
| `Behind.Common.Database` | Általános adatkezelési minták | IRepository, IUnitOfWork, DbContextExtensions |
| `Behind.Common.WebApi` | Általános API-segédeszközök | WebApplicationBuilder-bővítmények, OpenApi-konfiguráció, globális kezelők |
| `Behind.Core.Common` | Core tartományi segédeszközök | Email értékobjektumok, jelszó-kivonatolás, TOTP-konfiguráció |
| `Behind.Core.Domain` | Tartományi modellek (DDD) | User, Role, Grant aggregátumok, tartományi szabályok |
| `Behind.Core.Database` | Tartományspecifikus adatkezelés | User/Role DbContext, entitáskonfigurációk, felhasználói repository-k |
| `Behind.Core.WebApi` | Tartományspecifikus API | Felhasználói kontrollerek, hitelesítési végpontok, tartományspecifikus köztes szoftverek |

**Példa a kód elhelyezésére**:
```csharp
// ✓ HELYES: Általános szűrőbővítmény a Behind.Common projektben
extension(IQueryable<T>)
{
    public IQueryable<T> Apply(Filter<T> filter) => filter.Apply(this);
}

// ✓ HELYES: Általános repository a Behind.Common.Database projektben
public interface IRepository<TAggregate, TKey>
{
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
}

// ✓ HELYES: Felhasználóspecifikus repository a Behind.Core.Database projektben
public class UserRepository : IRepository<User>
{
    public async Task<Option<User>> TryFindByEmailAsync(Email email, ...)
    {
        return await dbSet.FirstOrDefaultAsync(u => u.Emails.Contains(email), ...);
    }
}
```

### 6. TARTSA A KÓDBÁZIST DRY ÉS TISZTA ELVEK SZERINT
**Szabály**: Ne duplikáljon kódot. Használja újra a meglévő kódot öröklődéssel, kompozícióval vagy kiemeléssel.

**Rossz (duplikáció)** ✗
```csharp
public class UserRepository
{
    public async Task<Option<User>> FindAsync(Guid id)
    {
        var user = await dbSet.FirstOrDefaultAsync(u => u.Id == id);
        return user == null ? None : Some(user);
    }
}

public class RoleRepository
{
    public async Task<Option<Role>> FindAsync(Guid id)
    {
        var role = await dbSet.FirstOrDefaultAsync(r => r.Id == id);
        return role == null ? None : Some(role);
    }
}
```

**Jó (újrahasznosítás)** ✓
```csharp
public interface IRepository<T, TKey>
{
    Task<Option<T>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
}

public class UserRepository : IRepository<User>
{
    public Task<Option<User>> TryFindAsync(Guid key, CancellationToken cancellationToken = default)
    {
        return dbSet.FirstOrDefaultAsync(u => u.Id == key, cancellationToken);
    }
}

public class RoleRepository : IRepository<Role>
{
    public Task<Option<Role>> TryFindAsync(Guid key, CancellationToken cancellationToken = default)
    {
        return dbSet.FirstOrDefaultAsync(r => r.Id == key, cancellationToken);
    }
}
```

### 7. KÖVESSE A SOLID ELVEKET
**Szabály**: A karbantarthatóság érdekében a kódot a SOLID elveknek megfelelően tervezze meg.

#### Egyetlen felelősség elve (Single Responsibility Principle, SRP)
Minden osztálynak egyetlen oka van a változásra.

```csharp
public class PasswordValidator
{
    public Validation<Password> Validate(string plainPassword)
    {
        // Csak a jelszószabályokat ellenőrzi
        // Nem kivonatol, nem hitelesít, nem tárol
    }
}

public class PasswordHasher
{
    public string Hash(string plainPassword) => BCrypt.HashPassword(plainPassword);
}

public class AuthenticationService
{
    public async Task<Exceptional<AuthToken>> AuthenticateAsync(Email email, string password)
    {
        // Összehangolja az ellenőrzést, a kivonatolást és a token előállítását
    }
}
```

#### Nyitott/zárt elv (Open/Closed Principle, OCP)
Nyitott a bővítésre, zárt a módosításra.

```csharp
public interface IAuthenticationStrategy
{
    Task<Exceptional<AuthToken>> AuthenticateAsync(Credentials creds);
}

public class PasswordAuthStrategy : IAuthenticationStrategy { }
public class TotpAuthStrategy : IAuthenticationStrategy { }
```

#### Liskov-féle helyettesítési elv (Liskov Substitution Principle, LSP)
Az altípusoknak helyettesíthetőnek kell lenniük az őstípusokkal.

```csharp
public abstract record User(Option<IEnumerable<Role>> Roles);
public sealed record HumanUser : User { }
public sealed record SystemUser : User { }
public sealed record GuestUser : User { }

// Minden User típust fogadó kód működik az összes altípussal
```

#### Interfészek szétválasztásának elve (Interface Segregation Principle, ISP)
Az ügyfelek ne függjenek olyan interfészektől, amelyeket nem használnak.

```csharp
// Jó: szétválasztott interfészek
public interface IReadable<T>
{
    Task<Option<T>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
}

public interface IWritable<T>
{
    Task AddAsync(T aggregate, CancellationToken cancellationToken = default);
    void Update(T aggregate);
    void Delete(T aggregate);
}

public interface IRepository<T> : IReadable<T>, IWritable<T> { }

// Az ügyfelek csak az IReadable-től függhetnek, ha csak olvasnak
public class UserReader
{
    private IReadable<User> repository;  // Csak olvas
}
```

#### Függőségek megfordításának elve (Dependency Inversion Principle, DIP)
Az absztrakcióktól függjön, ne a konkrét megvalósításoktól.

```csharp
public class UserService
{
    private readonly IRepository<User> repository;  // Absztrakció
    private readonly IPasswordHasher hasher;        // Absztrakció
    
    public UserService(IRepository<User> repository, IPasswordHasher hasher)
    {
        this.repository = repository;
        this.hasher = hasher;
    }
}
```

### 8. KÖVESSE A CLEAN CODE ELVEKET

**Értelmes nevek**:
```csharp
// Rossz ✗
public async Task<List<U>> GetUS(S s) { }

// Jó ✓
public async Task<IEnumerable<User>> GetUsersByStatusAsync(Status status) { }
```

**Kis függvények**:
```csharp
// Rossz: túl sok felelősség ✗
public async Task<User> ProcessUserRegistration(RegisterDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Email)) throw new ArgumentException();
    if (dto.Password.Length < 8) throw new ArgumentException();
    var hashedPassword = bcrypt.HashPassword(dto.Password);
    var user = new User { Email = dto.Email, Password = hashedPassword };
    await db.Users.AddAsync(user);
    await db.SaveChangesAsync();
    await emailService.SendWelcomeAsync(user);
    return user;
}

// Jó: egyetlen felelősség ✓
public async Task<User> RegisterUserAsync(RegisterDto dto)
{
    var validation = ValidateRegistration(dto);
    return await validation.Match(
        errors => throw new ValidationException(errors),
        valid => CreateAndPersistUser(valid)
    );
}

private Validation<ValidatedRegistration> ValidateRegistration(RegisterDto dto)
{
    return (
        from email in ValidateEmail(dto.Email)
        from password in ValidatePassword(dto.Password)
        select new ValidatedRegistration(email, password)
    );
}

private async Task<User> CreateAndPersistUser(ValidatedRegistration registration)
{
    var user = new User(
        Email: registration.Email,
        PasswordHash: hasher.Hash(registration.Password)
    );
    await repository.AddAsync(user);
    await emailService.SendWelcomeAsync(user);
    return user;
}
```

### 9. KÉSZÍTSEN TESZTEKET MINDEN ÚJ FUNKCIÓHOZ
**Szabály**: Minden új publikus metódusnak és funkciónak rendelkeznie kell egységtesztekkel.

**Tesztelési keretrendszer**: xUnit NSubstitute-tal

**Tesztek felépítése**:
```csharp
public class UserRepositoryTests
{
    [Fact]
    public async Task TryFindAsync_WithExistingUser_ReturnsUserInSome()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, "John", new[] { new Email("john@example.com") });
        var dbSet = CreateMockDbSet(user);
        var repository = new UserRepository(dbSet);

        // Act
        var result = await repository.TryFindAsync(userId);

        // Assert
        result.Match(
            () => Assert.Fail("Expected Some"),
            found => Assert.Equal(user, found)
        );
    }

    [Fact]
    public async Task TryFindAsync_WithNonExistentUser_ReturnsNone()
    {
        // Arrange
        var dbSet = CreateMockDbSet();
        var repository = new UserRepository(dbSet);

        // Act
        var result = await repository.TryFindAsync(Guid.NewGuid());

        // Assert
        result.Match(
            () => { /* Várt eredmény */ },
            _ => Assert.Fail("Expected None")
        );
    }
}
```

### 10. TypeDescriptor ÉS JsonConverterFactory HASZNÁLATA
**Szabály**: Az összetett típusok JSON-szerializálásához `TypeDescriptor`-t és `JsonConverterFactory`-t használjon.

**Hely**: Kövesse a `Behind.Common.Converters` és a `Behind.Core.WebApi` mintáit

**Példa**:
```csharp
namespace Behind.Common.Converters;

public class OptionJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(Option<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public class OptionJsonConverter<T> : JsonConverter<Option<T>>
{
    public override Option<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return None;
        
        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return value == null ? None : Some(value);
    }

    public override void Write(Utf8JsonWriter writer, Option<T> value, JsonSerializerOptions options)
    {
        value.Match(
            () => writer.WriteNullValue(),
            t => JsonSerializer.Serialize(writer, t, options)
        );
    }
}
```

## Elnevezési konvenciók

### Osztályok és rekordok
```csharp
public class UserRepository { }      // PascalCase
public record User { }                // PascalCase
public sealed record HumanUser { }    // PascalCase
```

### Metódusok
```csharp
public async Task<User> FindUserAsync() { }              // PascalCase, Async utótag az aszinkron metódusoknál
public Option<User> FindUser() { }                        // PascalCase
public async Task<Option<User>> TryFindAsync() { }        // PascalCase, Try előtag az opcionális visszatérési értékeknél
public Validation<User> ValidateUser() { }                // Validate előtag az ellenőrzésnél
```

### Változók és paraméterek
```csharp
var userId = Guid.NewGuid();          // camelCase
var currentUser = await FindUserAsync(userId);
```

### Konstansok
```csharp
public const int MaxPasswordLength = 128;    // PascalCase
public const string DefaultRole = "User";
```

### Bővítő névtér minta
```csharp
namespace Behind.Common;

extension(IQueryable<T>)  // A bővített típus a környezetben
{
    public IQueryable<T> Apply(Filter<T> filter) { }
}

extension(DbContext)
{
    public IImmutableEntityRepository<TEntity> ToImmutableEntityRepository<TEntity>(...) { }
}
```

## Kódszervezés

### Fájlszerkezet
```csharp
using System;
using System.Collections.Generic;  // Először a System névterek
using Behind.Common;                // Projekt névterek
using Behind.Common.Database;

namespace Behind.Core.Domain;       // A névtér megegyezik a mappaszerkezettel

public sealed record User : Aggregate  // Publikus típusok
{
    public Guid Id { get; init; }
    public Email Email { get; init; }
    
    public Validation<User> UpdateEmail(Email newEmail)
    {
        // megvalósítás
    }
}

public static class UserExtensions    // A bővítő osztályok a fájl végén
{
    extension(User)
    {
        public bool HasRole(Role role)
        {
            // megvalósítás
        }
    }
}
```

## Importok szervezése
1. System névterek
2. System.* bővítmények
3. Microsoft névterek
4. Harmadik féltől származó névterek
5. Projekt névterek (Behind.*)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Behind.Common;
using Behind.Core.Domain;
```

## Hozzáférés-módosítók

**Alapértelmezetten a legszűkebb hatókört válassza**:
- `public` - Csak a külső használatra szánt típusokhoz és tagokhoz
- `internal` - Az assembly-n belüli típusokhoz/tagokhoz
- `private` - Alapértelmezett a megvalósítási részletekhez
- `protected` - Csak akkor, ha az öröklődés megköveteli

```csharp
public record User
{
    public Guid Id { get; init; }                    // Publikus aggregátumazonosító
    public IEnumerable<Role> Roles { get; init; }    // Publikus aggregátumadat
    
    internal Email Email { get; init; }              // A tartományi rétegen belül belső
    private List<AuditLog> AuditLog { get; init; }   // Privát megvalósítási részlet
    
    public Validation<User> UpdateRoles(params Role[] roles)  // Publikus üzleti művelet
    {
        // megvalósítás
    }
}
```

## Hibakezelés

**Soha ne használjon puszta catch-et, és ne dobjon kivételeket a vezérlési folyamathoz**:

```csharp
// Rossz ✗
try
{
    var user = await repository.FindAsync(userId);  // kivételt dob, ha nem található
}
catch (NotFoundException) { }

// Jó ✓
var userOption = await repository.TryFindAsync(userId);  // Option<User>-t ad vissza
userOption.Match(
    () => logger.LogWarning("User not found"),
    user => ProcessUser(user)
);
```

## Naplózás

Használjon `ILogger<T>`-t állandó, PascalCase üzenetsablonokkal. Soha ne illesszen értékeket a sablonba.

```csharp
// Rossz ✗
logger.LogInformation($"User {user.Id} signed in");
logger.LogInformation("User {userId} signed in", user.Id);
logger.LogInformation("Request to {Path}", context.Request.Path.Value);

// Jó ✓
logger.LogInformation("Session {SessionId} started for user {UserId}", () => [sessionId, user.Id]);
```

Az argumentumokat `Func<object?[]>` formában kell átadni a `Behind.Common.Logging` névtérben található `LoggingExtensions` túlterhelésein keresztül (`using Behind.Common.Logging;`). A túlterhelések először az `IsEnabled` értékét ellenőrzik, így az argumentumok csak akkor értékelődnek ki, ha a szint engedélyezett. Használja őket minden olyan hívásnál, amelynek vannak argumentumai (CA1873).

| Szint | Mire használja | Példa |
|-------|---------|---------|
| `Debug` | Fejlesztés közben hasznos folyamatrészletek | `Skipping session validation for anonymous request to {Path}` |
| `Information` | Befejezett üzleti események | `User {UserId} elevated to {Role}` |
| `Warning` | A rendszer által kezelt elutasított vagy gyanús kérések | `Password sign-in rejected for user {UserId}: {Reason}` |
| `Error` | Hibák, mindig a kivétellel együtt | `logger.LogError(exception, "Failed to update user {UserId}", () => [user.Id])` |

- Azonosítókat (`{UserId}`, `{SessionId}`) naplózzon, soha ne naplózzon titkos adatokat, tokeneket, jelszavakat vagy varázslinkeket.
- A `Behind` névtér a `Development` környezetben `Debug` szinten naplóz.

## A követelmények összefoglalása

| Követelmény | Hogyan | Példa |
|-------------|-----|---------|
| Önmagát dokumentáló kód | Világos nevek, kis függvények | `TryFindUserAsync` a `Get` helyett |
| Bővítő metódusok | C# 12 `extension(Type)` szintaxis | `extension(IQueryable<T>)` |
| Változtathatatlanság | `record` és `init` használata | `public record Email(string Value);` |
| Aszinkron I/O | `async/await`, `CancellationToken` | `await repository.FindAsync(..., cancellationToken)` |
| Hibakezelés | Monádok (Option, Try, Exceptional, Validation) | `Option<User>` a `null` helyett |
| Kód elhelyezése | Megfelelő megosztott könyvtár | Az általános a Common-ban, a tartományi a Core.Domain-ben |
| Nincs duplikáció | Kiemelés a megfelelő rétegbe | Alap repository a Common.Database-ben |
| SOLID elvek | Interfész-absztrakciók, egyetlen felelősség | Szétválasztott interfészek, függőséginjektálás |
| Clean code | Kis függvények, értelmes nevek | Hosszú nevek > megjegyzések |
| Tesztelés | Egységtesztek minden publikus funkcionalitáshoz | Osztályonként egy tesztfájl a tests mappában |
| JSON-szerializálás | TypeDescriptor/JsonConverterFactory | Egyéni konverterek a Converters mappában |

---

**Ne feledje**: Úgy írja a kódot, mintha a karbantartója egy erőszakos pszichopata lenne, aki tudja, hol lakik. 😉

Következetesen kövesse ezeket a szabványokat egy professzionális, karbantartható kódbázis fenntartásához.

