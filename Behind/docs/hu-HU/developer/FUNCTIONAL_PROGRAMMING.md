# Funkcionális programozási minták

A Behind kódbázis kiterjedten használja a funkcionális programozási paradigmákat, különösen a vasútorientált programozást (Railway-Oriented Programming) és a monadikus kompozíciót. Ez a dokumentum ismerteti a mintákat, és azt, hogyan használhatja őket hatékonyan.

## Alapfilozófia

A Behind funkcionális programozása kiküszöböli a következőket:
- **Null hivatkozások** (az úgynevezett „milliárd dolláros hiba”)
- **Kivételalapú vezérlési folyamat** (kivételek az üzleti logikában)
- **Módosítható állapot** (alapértelmezés szerint megváltoztathatatlanság)
- **Mellékhatások** (ahol lehetséges, tiszta függvények)

Cserébe a következőket kapjuk:
- **Típusbiztonság** - A hibák a visszatérési típusokban jelennek meg
- **Összeállíthatóság** - A műveletek zökkenőmentesen láncolhatók
- **Kiszámíthatóság** - Nincsenek rejtett állapotváltozások
- **Tesztelhetőség** - A tiszta függvények könnyen tesztelhetők

## Alapvető monádok

### 1. Option<T> - A nullable típusok helyettesítője

**Cél**: Egy opcionális értéket jelképez – vagy Some(érték), vagy None.

**Felhasználási eset**: Az összes nullable referenciatípus kiváltása.

**Definíció**:
```csharp
public readonly struct Option<T> : IEquatable<Option<T>>
{
    public TR Match<TR>(Func<TR> none, Func<T, TR> some)
        => _isSome ? some(_value) : none();
    
    public static implicit operator Option<T>(None _) => new();
    public static implicit operator Option<T>(Some<T> some) => new(some.Value);
    public static implicit operator Option<T>(T value) => value == null ? None : Some(value);
}
```

**Létrehozás**:
```csharp
var some = Some(42);                    // Some<int>
var none = None;                        // None
var implicit = (Option<int>)null;       // None
var implicit2 = (Option<int>)42;        // Some(42)
var implicit3 = (Option<string>)"hello"; // Some("hello")
```

**Felhasználás** (vasútorientált programozás):
```csharp
public async Task<IActionResult> GetUser(Guid userId)
{
    var userOption = await repository.TryFindAsync(userId);
    
    return userOption.Match(
        none: () => NotFound($"User {userId} not found"),
        some: user => Ok(user)
    );
}
```

**Láncolás SelectMany-vel (LINQ)**:
```csharp
public async Task<Validation<UserDto>> GetUserDtoAsync(Guid userId)
{
    var userOption = await repository.TryFindAsync(userId);
    
    return userOption.Match(
        none: () => Invalid(new Error("User not found")),
        some: user => ValidateAndMapToDto(user)
    );
}
```

**Kompozíciós példa**:
```csharp
public class UserService
{
    public async Task<Option<UserProfile>> GetUserProfileAsync(Guid userId)
    {
        var userOption = await repository.TryFindAsync(userId);
        
        return await userOption.Match(
            none: async () => None,
            some: async user =>
                (await profileRepository.TryFindAsync(user.Id))
                    .Match(
                        none: () => None,
                        some: profile => Some(new UserProfile(user, profile))
                    )
        );
    }
}
```

**Fontos műveletek**:
```csharp
var option = Some(42);

// Átalakítás enumerable-lé
var enumerable = option.AsEnumerable();  // IEnumerable<int>

// Mintaillesztés
option.Match(
    () => "No value",
    x => $"Value: {x}"
);

// Implicit konverziók
Option<int> none = None;
Option<string> some = "hello";
```

### 2. Try<T> - Kivételek becsomagolása

**Cél**: Becsomagolja a kivételt dobható függvényeket, és a végrehajtást addig halasztja, amíg azt kifejezetten el nem indítják.

**Felhasználási eset**: Kivétellel megbukható műveletek összeállítása; lusta (lazy) végrehajtás.

**Definíció**:
```csharp
public delegate Exceptional<T> Try<T>();

public static class TryExtensions
{
    public static Exceptional<T> Run<T>(this Try<T> @try)
    {
        try { return @try(); }
        catch (Exception ex) { return ex; }
    }

    public static Try<R> Map<T, R>(this Try<T> @try, Func<T, R> f)
        => () => @try.Run().Match(ex => ex as Exceptional<R>, t => f(t));

    public static Try<R> Bind<T, R>(this Try<T> @try, Func<T, Try<R>> f)
        => () => @try.Run().Match(ex => ex as Exceptional<R>, t => f(t).Run());
}
```

**Létrehozás és használat**:
```csharp
var tryParseInt = Try(() => int.Parse("42"));  // Halasztva: még nem hajtódott végre

var result = tryParseInt.Run();  // Végrehajtás most, a kivételek elkapásával
result.Match(
    ex => Console.WriteLine($"Parse failed: {ex.Message}"),
    value => Console.WriteLine($"Parsed: {value}")
);
```

**Láncolás SelectMany-vel (LINQ)**:
```csharp
var query = 
    from x in Try(() => int.Parse("42"))
    from y in Try(() => int.Parse("8"))
    select x + y;

query.Run().Match(
    ex => Console.WriteLine($"Failed: {ex.Message}"),
    result => Console.WriteLine($"Result: {result}")  // 50
);
```

**Map átalakítás**:
```csharp
var transform = Try(() => "42")
    .Map(int.Parse)
    .Map(x => x * 2)
    .Map(x => x.ToString());

transform.Run().Match(
    ex => Console.WriteLine("Failed"),
    result => Console.WriteLine(result)  // "84"
);
```

**Gyakorlati példa**:
```csharp
public class JsonParser
{
    public Try<T> ParseJson<T>(string json)
        => Try(() => JsonSerializer.Deserialize<T>(json)!);
}

public class UserService
{
    private readonly JsonParser parser;

    public async Task<Exceptional<User>> ImportUserFromJsonAsync(string json)
    {
        return parser
            .ParseJson<UserDto>(json)
            .Map(dto => new User(dto.Email, dto.DisplayName))
            .Run();
    }
}
```

### 3. Exceptional<T> - Kivétel vagy sikeres eredmény

**Cél**: Vagy egy kivételt, vagy egy sikeres értéket jelképez; explicit hibatípus.

**Felhasználási eset**: Hibázhat művelet visszatérési típusa; lehetővé teszi az eredmények összeállítását.

**Definíció**:
```csharp
public struct Exceptional<T>
{
    internal Exception? Ex { get; }
    internal T? Value { get; }

    public bool Success => Ex == null;
    public bool Exception => Ex != null;

    public static implicit operator Exceptional<T>(Exception left) => new(left);
    public static implicit operator Exceptional<T>(T right) => new(right);

    public TR Match<TR>(Func<Exception, TR> exception, Func<T?, TR> success)
        => Ex is not null && Exception ? exception(Ex) : success(Value);
}
```

**Létrehozás**:
```csharp
public Exceptional<User> FindUser(Guid id)
{
    try
    {
        var user = dbSet.First(u => u.Id == id);
        return user;  // Implicit: T -> Exceptional<T>
    }
    catch (InvalidOperationException ex)
    {
        return ex;    // Implicit: Exception -> Exceptional<T>
    }
}
```

**Felhasználás**:
```csharp
var result = FindUser(userId);

result.Match(
    exception: ex => logger.LogError($"Error: {ex.Message}"),
    success: user => ProcessUser(user)
);
```

**Kompozíció kiterjesztő metódusokkal**:
```csharp
public static Exceptional<TR> Map<T, TR>(
    this Exceptional<T> @this, 
    Func<T, TR> f)
    => @this.Match(
        ex => ex as Exceptional<TR>,
        t => f(t)
    );

public static Exceptional<TR> Bind<T, TR>(
    this Exceptional<T> @this,
    Func<T, Exceptional<TR>> f)
    => @this.Match(
        ex => ex as Exceptional<TR>,
        t => f(t)
    );

public Exceptional<AuthToken> Authenticate(Credentials creds)
{
    return FindUser(creds.Email)
        .Map(user => ValidatePassword(user, creds.Password))
        .Map(user => GenerateToken(user));
}
```

**Különbség az Option-höz képest**:
```csharp
public Option<User> FindUserOptional(Guid id)       // A kivétel részletei elvesznek
{
    return dbSet.FirstOrDefault(u => u.Id == id);
}

public Exceptional<User> FindUserExceptional(Guid id) // Megőrzi a kivételt
{
    try
    {
        return dbSet.First(u => u.Id == id);
    }
    catch (Exception ex)
    {
        return ex;
    }
}
```

### 4. Validation<T> - Hibák felhalmozása

**Cél**: Az összes validációs hibát összegyűjti, ahelyett hogy az elsőnél megállna.

**Felhasználási eset**: Űrlapok ellenőrzése, parancsok ellenőrzése, több mezős validáció.

**Definíció**:
```csharp
public readonly struct Validation<T> : IEquatable<Validation<T>>
{
    internal IEnumerable<Error> Errors { get; }
    private readonly T? _value;
    public bool IsValid { get; }

    public TR Match<TR>(Func<IEnumerable<Error>, TR> invalid, Func<T, TR> valid)
        => IsValid && _value is not null ? valid(_value) : invalid(Errors);
}

public record Error(string Code, string Message);
```

**Létrehozás**:
```csharp
public Validation<int> ValidateAge(int age)
{
    if (age < 0)
        return Invalid(new Error("AGE_NEGATIVE", "Age cannot be negative"));
    
    if (age > 150)
        return Invalid(new Error("AGE_TOO_HIGH", "Age is unrealistic"));
    
    return Valid(age);  // Siker
}
```

**Több validáció felhalmozása**:
```csharp
public Validation<RegisteredUser> ValidateRegistration(RegisterDto dto)
{
    return (
        from email in ValidateEmail(dto.Email)
        from password in ValidatePassword(dto.Password)
        from age in ValidateAge(dto.Age)
        select new RegisteredUser(email, password, age)
    );
}

private Validation<Email> ValidateEmail(string email)
{
    var errors = new List<Error>();
    
    if (string.IsNullOrWhiteSpace(email))
        errors.Add(new Error("EMAIL_REQUIRED", "Email is required"));
    
    if (!email.Contains("@"))
        errors.Add(new Error("EMAIL_INVALID", "Email must contain @"));
    
    return errors.Count > 0 ? Invalid(errors) : Valid(new Email(email));
}

private Validation<Password> ValidatePassword(string password)
{
    var errors = new List<Error>();
    
    if (password.Length < 8)
        errors.Add(new Error("PASSWORD_TOO_SHORT", "Password must be at least 8 characters"));
    
    if (!password.Any(char.IsUpper))
        errors.Add(new Error("PASSWORD_NO_UPPER", "Password must contain uppercase letter"));
    
    if (!password.Any(char.IsDigit))
        errors.Add(new Error("PASSWORD_NO_DIGIT", "Password must contain digit"));
    
    return errors.Count > 0 ? Invalid(errors) : Valid(new Password(password));
}
```

**Használat API végpontokban**:
```csharp
[HttpPost("register")]
public async Task<IActionResult> RegisterAsync(RegisterDto dto)
{
    var validation = ValidateRegistration(dto);
    
    return await validation.Match(
        invalid: errors => BadRequest(new
        {
            errors = errors.Select(e => new { e.Code, e.Message })
        }),
        valid: async registration =>
        {
            var user = new User(registration.Email, registration.Password, registration.Age);
            await repository.AddAsync(user);
            return CreatedAtRoute("GetUser", new { id = user.Id }, user);
        }
    );
}
```

**Több validáció kombinálása**:
```csharp
public Validation<UpdateUserCommand> ValidateUpdate(UpdateUserDto dto)
{
    return Validation.Collect(
        ValidateName(dto.Name),
        ValidateEmail(dto.Email),
        ValidateBirthDate(dto.BirthDate)
    );
}
```

## Funkcionális kiterjesztések

### Pipe és Tap

**Pipe**: Egy értéket átvezet egy függvényen.

```csharp
var result = user
    .Pipe(u => ValidateUser(u))      // A felhasználó átadása az ellenőrzésnek
    .Pipe(validation => validation   // Folytatás az eredménnyel
        .Match(
            invalid => HandleErrors(invalid),
            valid => PersistUser(valid)
        )
    );
```

**Tap**: Végrehajt egy mellékhatást, és visszaadja az eredeti értéket.

```csharp
var persistedUser = user
    .Pipe(u => logger.Log($"Creating user {u.Email}"), u => u)  // Naplózási mellékhatás
    .Pipe(u => repository.Add(u), u => u)                        // Hozzáadás a repository-hoz
    .Pipe(u => logger.Log($"User created with ID {u.Id}"), u => u);
```

### Curry és kompozíció

**Curry**: A többparaméteres függvényt egymásba ágyazott, egyparaméteres függvényekké alakítja.

```csharp
// Normál függvény
public string CreateGreeting(string greeting, string name, string punctuation)
    => $"{greeting} {name}{punctuation}";

// Curry-zott
var greetCurried = CreateGreeting.Curry();

var hello = greetCurried("Hello")("Alice")("!");  // "Hello Alice!"
var greetFormal = greetCurried("Good morning");    // Részleges alkalmazás
var greetFormalJohn = greetFormal("John")(".");    // "Good morning John."
```

**CurryFirst**: Csak az első paramétert curry-zi.

```csharp
public decimal CalculatePrice(decimal basePrice, decimal taxRate, decimal discount)
    => (basePrice * (1 + taxRate)) * (1 - discount);

var curried = CalculatePrice.CurryFirst();
var priceCalculator = curried(100m);  // Alapár: 100

var withTax = priceCalculator(0.1m, 0m);        // Adó hozzáadása, kedvezmény nélkül
var withTaxAndDiscount = priceCalculator(0.1m, 0.1m);  // Mindkettő hozzáadása
```

## Vasútorientált programozás

A vasútorientált programozás (Railway-Oriented Programming, ROP) a számításokat vasúti pályaként kezeli: van egy sikerág és egy hibaág.

### Kétsínes modell

```csharp
public Exceptional<User> AuthenticateUser(Credentials creds)
{
    // A pálya minden lépésnél elágazik
    return ValidateCredentials(creds)           // Főág vagy hibaág
        .Map(ValidateNotBanned)                  // Marad a pályán, vagy átvált a hibaágra
        .Map(GenerateAuthToken);                 // Végső siker a főágon
}

private Exceptional<Credentials> ValidateCredentials(Credentials creds)
{
    if (string.IsNullOrEmpty(creds.Email))
        return new ArgumentException("Email required");
    
    if (string.IsNullOrEmpty(creds.Password))
        return new ArgumentException("Password required");
    
    return creds;  // Marad a sikerágon
}

private User ValidateNotBanned(User user)
{
    if (user is BannedUser)
        throw new InvalidOperationException("User is banned");
    
    return user;
}

private AuthToken GenerateAuthToken(User user)
{
    return new AuthToken(user.Id, DateTime.UtcNow.AddHours(24));
}
```

### Háromsínes modell

A Validation<T> használata a hibák felhalmozására:

```
    Invalid      Main        Success
      |-----------|-----------|
             |          |
          Errors      Value
```

```csharp
public async Task<IActionResult> RegisterUserAsync(RegisterDto dto)
{
    var validation = ValidateRegistration(dto);
    
    return await validation.Match(
        invalid: errors =>                      // Bal oldali ág
        {
            return BadRequest(new
            {
                message = "Registration validation failed",
                errors = errors.Select(e => new { e.Code, e.Message })
            });
        },
        valid: async registration =>            // Jobb oldali ág
        {
            var user = new User(registration);
            await repository.AddAsync(user);
            return CreatedAtRoute("GetUser", new { id = user.Id });
        }
    );
}
```

## Kompozíciós minták

### Map és Bind

**Map** (Functor): Átalakítja a becsomagolt értéket.

```csharp
var option = Some(42);
var result = option.Map(x => x * 2);  // Some(84)

var none = None;
var result2 = none.Map(x => x * 2);   // None (a map figyelmen kívül marad)
```

**Bind** (Monad): Becsomagolt értéket visszaadó műveleteket láncol.

```csharp
public Exceptional<User> GetUserWithRoles(Guid userId)
{
    return FindUser(userId)
        .Bind(user => LoadRoles(user.Id)      // Exceptional<Role[]>-t ad vissza
            .Map(roles => user with { Roles = roles }));
}

private Exceptional<Role[]> LoadRoles(Guid userId)
{
    try
    {
        return roleRepository.GetByUserId(userId);
    }
    catch (Exception ex)
    {
        return ex;
    }
}
```

### LINQ szintaxis (SelectMany)

Összetett láncoknál ez a legolvashatóbb:

```csharp
var result =
    from user in FindUserAsync(userId)
    from roles in LoadRolesAsync(user.Id)
    from permissions in LoadPermissionsAsync(roles.Select(r => r.Id))
    select new UserDetails(user, roles, permissions);
```

A fordító ezt a következőre bontja:

```csharp
var result = FindUserAsync(userId)
    .SelectMany(
        user => LoadRolesAsync(user.Id),
        (user, roles) => new { user, roles }
    )
    .SelectMany(
        x => LoadPermissionsAsync(x.roles.Select(r => r.Id)),
        (x, permissions) => new UserDetails(x.user, x.roles, permissions)
    );
```

## Gyakori minták

### Nullable referenciák kezelése

**Előtte (null értékekkel)**:
```csharp
public async Task<IActionResult> GetUserAsync(Guid userId)
{
    var user = await repository.FindAsync(userId);
    if (user == null)
        return NotFound();
    
    return Ok(user);
}
```

**Utána (Option-nel)**:
```csharp
public async Task<IActionResult> GetUserAsync(Guid userId)
{
    var userOption = await repository.TryFindAsync(userId);
    
    return userOption.Match(
        () => NotFound(),
        user => Ok(user)
    );
}
```

### Kivételkezelés

**Előtte (try/catch-csel)**:
```csharp
public Guid RegisterUser(RegisterDto dto)
{
    try
    {
        var validation = validator.Validate(dto);
        var user = mapper.Map<User>(dto);
        repository.Add(user);
        emailService.SendWelcome(user);
        return user.Id;
    }
    catch (ValidationException ex)
    {
        logger.LogError($"Validation failed: {ex}");
        throw;
    }
    catch (DbException ex)
    {
        logger.LogError($"Database error: {ex}");
        throw;
    }
}
```

**Utána (Exceptional-lal)**:
```csharp
public async Task<Exceptional<Guid>> RegisterUserAsync(RegisterDto dto)
{
    return await Try(() => validator.Validate(dto))
        .Map(v => mapper.Map<User>(dto))
        .Bind(user => repository.AddAsync(user))
        .Bind(user => emailService.SendWelcomeAsync(user))
        .Map(user => user.Id)
        .Run()
        .Apply(async userId => await NotifyAdminAsync(userId), userId => userId);
}
```

### Feltételes logika

**Előtte (if/else-szel)**:
```csharp
public string GetUserDisplayName(User user)
{
    if (user is HumanUser human)
    {
        return human.DisplayName;
    }
    else if (user is SystemUser)
    {
        return "System";
    }
    else if (user is GuestUser)
    {
        return "Guest";
    }
    else
    {
        return "Unknown";
    }
}
```

**Utána (record-okon végzett mintaillesztéssel)**:
```csharp
public string GetUserDisplayName(User user) =>
    user switch
    {
        HumanUser human => human.DisplayName,
        SystemUser => "System",
        GuestUser => "Guest",
        _ => "Unknown"
    };
```

## Előnyök összefoglalása

| Minta | Előny |
|-------|-------|
| Option<T> | Nincs több null-ellenőrzés, a fordító kikényszeríti |
| Try<T> | Lusta kivételkezelés, összeállítható |
| Exceptional<T> | A hiba környezete megmarad, explicit hibatípusok |
| Validation<T> | Az összes hibát összegyűjti, jobb felhasználói élmény |
| Pipe/Tap | Olvasható műveletlánc |
| LINQ szintaxis | Ismerős a C# fejlesztők számára |
| Vasútorientált | Szemléletes metafora a hibakezeléshez |

## Hivatkozások

- **Vasútorientált programozás**: https://fsharpforfunandprofit.com/posts/recipe-part2/
- **Monadikus kompozíció**: https://en.wikipedia.org/wiki/Monad_(functional_programming)
- **Típusvezérelt fejlesztés**: A kódot tesztek helyett a típusok vezérelte módon írjuk

---

**Gyakorlati tippek**:
1. Kezdje azzal, hogy a `null` értékeket `Option<T>`-re cseréli
2. A `try/catch` blokkokat cserélje `Try<T>`-re vagy `Exceptional<T>`-re
3. Minden felhasználói bemenet ellenőrzéséhez használja a `Validation<T>`-t
4. Éljen a mintaillesztés lehetőségeivel az eredményeken
5. Gondolkodjon boldog útvonalban (főág) és hibaútvonalakban

Minél többet használja ezeket a mintákat, annál természetesebbé válnak. A kódja biztonságosabb, jobban összeállítható és könnyebben tesztelhető lesz.
