# Functional Programming Patterns

The Behind codebase makes extensive use of functional programming paradigms, particularly Railway-Oriented Programming and monadic composition. This document explains the patterns and how to use them effectively.

## Core Philosophy

Functional programming in Behind eliminates:
- **Null references** (the "Billion Dollar Mistake")
- **Exception-based control flow** (exceptions for business logic)
- **Mutable state** (immutability by default)
- **Side effects** (pure functions where possible)

In exchange, we get:
- **Type safety** - Errors encoded in return types
- **Composability** - Chain operations seamlessly
- **Predictability** - No hidden state changes
- **Testability** - Pure functions are easy to test

## Core Monads

### 1. Option<T> - Nullable Replacement

**Purpose**: Represents an optional value - either Some(value) or None.

**Use Case**: Replace all nullable reference types.

**Definition**:
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

**Creation**:
```csharp
var some = Some(42);                    // Some<int>
var none = None;                        // None
var implicit = (Option<int>)null;       // None
var implicit2 = (Option<int>)42;        // Some(42)
var implicit3 = (Option<string>)"hello"; // Some("hello")
```

**Consumption** (Railway-Oriented Programming):
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

**Chaining with SelectMany (LINQ)**:
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

**Composition Example**:
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

**Key Operations**:
```csharp
var option = Some(42);

// Convert to enumerable
var enumerable = option.AsEnumerable();  // IEnumerable<int>

// Pattern matching
option.Match(
    () => "No value",
    x => $"Value: {x}"
);

// Implicit conversions
Option<int> none = None;
Option<string> some = "hello";
```

### 2. Try<T> - Exception Wrapping

**Purpose**: Wraps functions that might throw exceptions, deferring execution until explicitly run.

**Use Case**: Compose operations that might fail with exceptions; execute lazily.

**Definition**:
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

**Creation and Usage**:
```csharp
var tryParseInt = Try(() => int.Parse("42"));  // Deferred: not executed yet

var result = tryParseInt.Run();  // Execute now and catch exceptions
result.Match(
    ex => Console.WriteLine($"Parse failed: {ex.Message}"),
    value => Console.WriteLine($"Parsed: {value}")
);
```

**Chaining with SelectMany (LINQ)**:
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

**Map Transformation**:
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

**Practical Example**:
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

### 3. Exceptional<T> - Exception vs Success Result

**Purpose**: Represents either an Exception or a Success value; explicit error type.

**Use Case**: Function return type for operations that might fail; enables result composition.

**Definition**:
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

**Creation**:
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

**Consumption**:
```csharp
var result = FindUser(userId);

result.Match(
    exception: ex => logger.LogError($"Error: {ex.Message}"),
    success: user => ProcessUser(user)
);
```

**Composition with Extension Methods**:
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

**Difference from Option**:
```csharp
public Option<User> FindUserOptional(Guid id)       // Loses exception details
{
    return dbSet.FirstOrDefault(u => u.Id == id);
}

public Exceptional<User> FindUserExceptional(Guid id) // Preserves exception
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

### 4. Validation<T> - Accumulating Errors

**Purpose**: Accumulates all validation errors rather than failing on the first one.

**Use Case**: Form validation, command validation, multi-field validation.

**Definition**:
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

**Creation**:
```csharp
public Validation<int> ValidateAge(int age)
{
    if (age < 0)
        return Invalid(new Error("AGE_NEGATIVE", "Age cannot be negative"));
    
    if (age > 150)
        return Invalid(new Error("AGE_TOO_HIGH", "Age is unrealistic"));
    
    return Valid(age);  // Success
}
```

**Accumulating Multiple Validations**:
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

**Usage in API Endpoints**:
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

**Combining Multiple Validations**:
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

## Functional Extensions

### Pipe and Tap

**Pipe**: Thread a value through a function.

```csharp
var result = user
    .Pipe(u => ValidateUser(u))      // Pass user through validation
    .Pipe(validation => validation   // Continue with result
        .Match(
            invalid => HandleErrors(invalid),
            valid => PersistUser(valid)
        )
    );
```

**Tap**: Execute a side effect and return the original value.

```csharp
var persistedUser = user
    .Pipe(u => logger.Log($"Creating user {u.Email}"), u => u)  // Log side effect
    .Pipe(u => repository.Add(u), u => u)                        // Add to repo
    .Pipe(u => logger.Log($"User created with ID {u.Id}"), u => u);
```

### Curry and Composition

**Curry**: Convert multi-parameter function to nested single-parameter functions.

```csharp
// Normal function
public string CreateGreeting(string greeting, string name, string punctuation)
    => $"{greeting} {name}{punctuation}";

// Curried
var greetCurried = CreateGreeting.Curry();

var hello = greetCurried("Hello")("Alice")("!");  // "Hello Alice!"
var greetFormal = greetCurried("Good morning");    // Partial application
var greetFormalJohn = greetFormal("John")(".");    // "Good morning John."
```

**CurryFirst**: Curry only the first parameter.

```csharp
public decimal CalculatePrice(decimal basePrice, decimal taxRate, decimal discount)
    => (basePrice * (1 + taxRate)) * (1 - discount);

var curried = CalculatePrice.CurryFirst();
var priceCalculator = curried(100m);  // Base price 100

var withTax = priceCalculator(0.1m, 0m);        // Add tax, no discount
var withTaxAndDiscount = priceCalculator(0.1m, 0.1m);  // Add both
```

## Railway-Oriented Programming

Railway-Oriented Programming (ROP) treats computations as rail tracks: success track and failure track.

### Two-Track Model

```csharp
public Exceptional<User> AuthenticateUser(Credentials creds)
{
    // Track splits at each step
    return ValidateCredentials(creds)           // Main track or error track
        .Map(ValidateNotBanned)                  // Stay on track or switch to error
        .Map(GenerateAuthToken);                 // Final success on main track
}

private Exceptional<Credentials> ValidateCredentials(Credentials creds)
{
    if (string.IsNullOrEmpty(creds.Email))
        return new ArgumentException("Email required");
    
    if (string.IsNullOrEmpty(creds.Password))
        return new ArgumentException("Password required");
    
    return creds;  // Stay on success track
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

### Three-Track Model

Using Validation<T> for accumulating errors:

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
        invalid: errors =>                      // Left track
        {
            return BadRequest(new
            {
                message = "Registration validation failed",
                errors = errors.Select(e => new { e.Code, e.Message })
            });
        },
        valid: async registration =>            // Right track
        {
            var user = new User(registration);
            await repository.AddAsync(user);
            return CreatedAtRoute("GetUser", new { id = user.Id });
        }
    );
}
```

## Composition Patterns

### Map vs Bind

**Map** (Functor): Transform the wrapped value.

```csharp
var option = Some(42);
var result = option.Map(x => x * 2);  // Some(84)

var none = None;
var result2 = none.Map(x => x * 2);   // None (map ignored)
```

**Bind** (Monad): Chain operations that return wrapped values.

```csharp
public Exceptional<User> GetUserWithRoles(Guid userId)
{
    return FindUser(userId)
        .Bind(user => LoadRoles(user.Id)      // Returns Exceptional<Role[]>
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

### LINQ Syntax (SelectMany)

Most readable for complex chains:

```csharp
var result =
    from user in FindUserAsync(userId)
    from roles in LoadRolesAsync(user.Id)
    from permissions in LoadPermissionsAsync(roles.Select(r => r.Id))
    select new UserDetails(user, roles, permissions);
```

Desugars to:

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

## Common Patterns

### Nullable Reference Handling

**Before (with nulls)**:
```csharp
public async Task<IActionResult> GetUserAsync(Guid userId)
{
    var user = await repository.FindAsync(userId);
    if (user == null)
        return NotFound();
    
    return Ok(user);
}
```

**After (with Option)**:
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

### Exception Handling

**Before (with try/catch)**:
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

**After (with Exceptional)**:
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

### Conditional Logic

**Before (with if/else)**:
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

**After (with pattern matching on records)**:
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

## Benefits Summary

| Pattern | Benefit |
|---------|---------|
| Option<T> | No more null checks, compiler-enforced |
| Try<T> | Lazy exception handling, composable |
| Exceptional<T> | Preserve error context, explicit error types |
| Validation<T> | Collect all errors, better UX |
| Pipe/Tap | Readable chain of operations |
| LINQ Syntax | Familiar to C# developers |
| Railway-Oriented | Visual metaphor for error handling |

## References

- **Railway-Oriented Programming**: https://fsharpforfunandprofit.com/posts/recipe-part2/
- **Monadic Composition**: https://en.wikipedia.org/wiki/Monad_(functional_programming)
- **Type-Driven Development**: Write code guided by types instead of tests

---

**Practice Tips**:
1. Start by replacing `null` with `Option<T>`
2. Replace `try/catch` blocks with `Try<T>` or `Exceptional<T>`
3. Use `Validation<T>` for all user input validation
4. Embrace pattern matching on results
5. Think in terms of happy path (main track) and error paths

The more you use these patterns, the more natural they become. Your code will be safer, more composable, and easier to test.
