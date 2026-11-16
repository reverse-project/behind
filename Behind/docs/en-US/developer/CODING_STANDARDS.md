# Coding Standards and Conventions

This document outlines the mandatory coding standards that all developers must follow. These standards ensure consistency, maintainability, and code quality across the Behind codebase.

## 🚫 Golden Rules

### 1. NO COMMENTS IN CODE
**Rule**: If code needs comments, it's bad code. Code must be self-documenting.

**What This Means**:
- Use clear, descriptive names for variables, methods, and classes
- Write small, focused functions with single responsibilities
- Use strong typing to convey intent
- Organize code logically with meaningful structure

**Bad Example** ✗
```csharp
public class UserService
{
    // Check if user exists by looking up in database
    public async Task<bool> CheckUser(string id)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        return u != null; // Found user
    }
}
```

**Good Example** ✓
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

### 1a. NO BRACE-LESS CONTROL STATEMENTS
**Rule**: Never use shortened `if`, `else`, `for`, `foreach`, `while` or `using` statements without braces. Always use a braced block with the body on its own lines. This prevents bugs like Apple's "goto fail".

**Bad Example** ✗
```csharp
if (session is null) return None;
```

**Good Example** ✓
```csharp
if (session is null)
{
    return None;
}
```

### 2. USE EXTENSION MEMBERS WITH C# 14+ SYNTAX
**Rule**: Use the `extension(Type)` syntax for extension members.

**File Naming**: All extension files must end with `Extensions` (e.g., `StringExtensions.cs`, `DbContextExtensions.cs`)

**Syntax**:
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
        // implementation
        return jsonObject;
    }
}
```

**Why**: Extension members keep extension declarations explicit and close to their receiver type.

### 3. USE RECORDS, IMMUTABLE OBJECTS, AND ASYNC/AWAIT
**Rule**: Favor immutability and async patterns for all I/O and data structures.

**Record Usage**:
```csharp
public record Email(string Value);
public record Permission(string Name, Option<IEnumerable<Grant>> Grants);
public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }
    public string DisplayName { get; init; }
}
```

**Immutability Benefits**:
- Thread-safe by default
- Eliminates entire classes of bugs
- Clear value semantics
- Built-in equality

**Async/Await Everywhere**:
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

### 4. USE FUNCTIONAL TOOLS AND MONADS
**Rule**: Use functional programming constructs (Option, Try, Exceptional, Validation) instead of nulls and exceptions for control flow.

**Available Monads**:

| Monad | Purpose | Example |
|-------|---------|---------|
| `Option<T>` | Nullable replacement | `Option<User> user` |
| `Try<T>` | Exception wrapping | `Try<Auth> result` |
| `Exceptional<T>` | Exception vs Success | `public Exceptional<Auth> Authenticate(...)` |
| `Validation<T>` | Accumulating errors | `Validation<Command> ValidateCommand(...)` |

**Examples**:

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

### 5. IMPLEMENT IN APPROPRIATE LAYERS
**Rule**: Place common code in the appropriate shared library.

**Layer Guidelines**:

| Layer | Purpose | Examples |
|-------|---------|----------|
| `Behind.Common` | Reusable abstractions, utilities | Option, Try, Validation, functional extensions, filters |
| `Behind.Common.Database` | Generic data patterns | IRepository, IUnitOfWork, DbContextExtensions |
| `Behind.Common.WebApi` | Generic API utilities | WebApplicationBuilder extensions, OpenApi config, global handlers |
| `Behind.Core.Common` | Core domain utilities | Email value objects, password hashing, TOTP config |
| `Behind.Core.Domain` | Domain models (DDD) | User, Role, Grant aggregates, domain rules |
| `Behind.Core.Database` | Domain-specific data | User/Role DbContext, entity configurations, user repositories |
| `Behind.Core.WebApi` | Domain-specific API | User controllers, auth endpoints, domain-specific middleware |

**Code Placement Example**:
```csharp
// ✓ CORRECT: Generic filter extension in Behind.Common
extension(IQueryable<T>)
{
    public IQueryable<T> Apply(Filter<T> filter) => filter.Apply(this);
}

// ✓ CORRECT: Generic repository in Behind.Common.Database
public interface IRepository<TAggregate, TKey>
{
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
}

// ✓ CORRECT: User-specific repository in Behind.Core.Database
public class UserRepository : IRepository<User>
{
    public async Task<Option<User>> TryFindByEmailAsync(Email email, ...)
    {
        return await dbSet.FirstOrDefaultAsync(u => u.Emails.Contains(email), ...);
    }
}
```

### 6. KEEP CODEBASE DRY AND CLEAN
**Rule**: Don't duplicate code. Reuse existing code through inheritance, composition, or extraction.

**Bad (Duplication)** ✗
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

**Good (Reuse)** ✓
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

### 7. FOLLOW SOLID PRINCIPLES
**Rule**: Design code adhering to SOLID principles for maintainability.

#### Single Responsibility Principle (SRP)
Each class has one reason to change.

```csharp
public class PasswordValidator
{
    public Validation<Password> Validate(string plainPassword)
    {
        // Only validates password rules
        // Doesn't hash, doesn't authenticate, doesn't store
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
        // Orchestrates validation, hashing, token generation
    }
}
```

#### Open/Closed Principle (OCP)
Open for extension, closed for modification.

```csharp
public interface IAuthenticationStrategy
{
    Task<Exceptional<AuthToken>> AuthenticateAsync(Credentials creds);
}

public class PasswordAuthStrategy : IAuthenticationStrategy { }
public class TotpAuthStrategy : IAuthenticationStrategy { }
```

#### Liskov Substitution Principle (LSP)
Subtypes must be substitutable for base types.

```csharp
public abstract record User(Option<IEnumerable<Role>> Roles);
public sealed record HumanUser : User { }
public sealed record SystemUser : User { }
public sealed record GuestUser : User { }

// Any code accepting User works with all subtypes
```

#### Interface Segregation Principle (ISP)
Clients shouldn't depend on interfaces they don't use.

```csharp
// Good: Segregated interfaces
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

// Clients can depend on just IReadable if they only read
public class UserReader
{
    private IReadable<User> repository;  // Only reads
}
```

#### Dependency Inversion Principle (DIP)
Depend on abstractions, not concretions.

```csharp
public class UserService
{
    private readonly IRepository<User> repository;  // Abstraction
    private readonly IPasswordHasher hasher;        // Abstraction
    
    public UserService(IRepository<User> repository, IPasswordHasher hasher)
    {
        this.repository = repository;
        this.hasher = hasher;
    }
}
```

### 8. FOLLOW CLEAN CODE PRINCIPLES

**Meaningful Names**:
```csharp
// Bad ✗
public async Task<List<U>> GetUS(S s) { }

// Good ✓
public async Task<IEnumerable<User>> GetUsersByStatusAsync(Status status) { }
```

**Small Functions**:
```csharp
// Bad: Too many responsibilities ✗
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

// Good: Single responsibility ✓
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

### 9. CREATE TESTS FOR ALL NEW FUNCTIONALITIES
**Rule**: All new public methods and features must have unit tests.

**Testing Framework**: xUnit with NSubstitute

**Test Structure**:
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
            () => { /* Expected */ },
            _ => Assert.Fail("Expected None")
        );
    }
}
```

### 10. USE TypeDescriptor AND JsonConverterFactory
**Rule**: Use `TypeDescriptor` and `JsonConverterFactory` for JSON serialization of complex types.

**Location**: Follow patterns in `Behind.Common.Converters` and `Behind.Core.WebApi`

**Example**:
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

## Naming Conventions

### Classes and Records
```csharp
public class UserRepository { }      // PascalCase
public record User { }                // PascalCase
public sealed record HumanUser { }    // PascalCase
```

### Methods
```csharp
public async Task<User> FindUserAsync() { }              // PascalCase, Async suffix for async methods
public Option<User> FindUser() { }                        // PascalCase
public async Task<Option<User>> TryFindAsync() { }        // PascalCase, Try prefix for optional returns
public Validation<User> ValidateUser() { }                // Validate prefix for validation
```

### Variables and Parameters
```csharp
var userId = Guid.NewGuid();          // camelCase
var currentUser = await FindUserAsync(userId);
```

### Constants
```csharp
public const int MaxPasswordLength = 128;    // PascalCase
public const string DefaultRole = "User";
```

### Extension Namespace Pattern
```csharp
namespace Behind.Common;

extension(IQueryable<T>)  // Extended type in context
{
    public IQueryable<T> Apply(Filter<T> filter) { }
}

extension(DbContext)
{
    public IImmutableEntityRepository<TEntity> ToImmutableEntityRepository<TEntity>(...) { }
}
```

## Code Organization

### File Structure
```csharp
using System;
using System.Collections.Generic;  // System namespaces first
using Behind.Common;                // Project namespaces
using Behind.Common.Database;

namespace Behind.Core.Domain;       // Namespace matches folder structure

public sealed record User : Aggregate  // Public types
{
    public Guid Id { get; init; }
    public Email Email { get; init; }
    
    public Validation<User> UpdateEmail(Email newEmail)
    {
        // implementation
    }
}

public static class UserExtensions    // Extension classes at end of file
{
    extension(User)
    {
        public bool HasRole(Role role)
        {
            // implementation
        }
    }
}
```

## Import Organization
1. System namespaces
2. System.* extensions
3. Microsoft namespaces
4. Third-party namespaces
5. Project namespaces (Behind.*)

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

## Access Modifiers

**Default to smallest scope**:
- `public` - Only for types and members intended for external use
- `internal` - For types/members within the assembly
- `private` - Default for implementation details
- `protected` - Only when inheritance requires

```csharp
public record User
{
    public Guid Id { get; init; }                    // Public aggregate identifier
    public IEnumerable<Role> Roles { get; init; }    // Public aggregate data
    
    internal Email Email { get; init; }              // Internal to domain layer
    private List<AuditLog> AuditLog { get; init; }   // Private implementation detail
    
    public Validation<User> UpdateRoles(params Role[] roles)  // Public business operation
    {
        // implementation
    }
}
```

## Error Handling

**Never use bare catch or throw exceptions for control flow**:

```csharp
// Bad ✗
try
{
    var user = await repository.FindAsync(userId);  // throws if not found
}
catch (NotFoundException) { }

// Good ✓
var userOption = await repository.TryFindAsync(userId);  // returns Option<User>
userOption.Match(
    () => logger.LogWarning("User not found"),
    user => ProcessUser(user)
);
```

## Logging

Use `ILogger<T>` with constant, PascalCase message templates. Never interpolate values into the template.

```csharp
// Bad ✗
logger.LogInformation($"User {user.Id} signed in");
logger.LogInformation("User {userId} signed in", user.Id);
logger.LogInformation("Request to {Path}", context.Request.Path.Value);

// Good ✓
logger.LogInformation("Session {SessionId} started for user {UserId}", () => [sessionId, user.Id]);
```

Arguments are passed as a `Func<object?[]>` through the `LoggingExtensions` overloads in `Behind.Common.Logging` (`using Behind.Common.Logging;`). The overloads check `IsEnabled` first, so the arguments are only evaluated when the level is enabled. Use them for every call that has arguments (CA1873).

| Level | Use for | Example |
|-------|---------|---------|
| `Debug` | Flow details useful while developing | `Skipping session validation for anonymous request to {Path}` |
| `Information` | Completed business events | `User {UserId} elevated to {Role}` |
| `Warning` | Rejected or suspicious requests the system handled | `Password sign-in rejected for user {UserId}: {Reason}` |
| `Error` | Failures, always with the exception | `logger.LogError(exception, "Failed to update user {UserId}", () => [user.Id])` |

- Log identifiers (`{UserId}`, `{SessionId}`), never secrets, tokens, passwords or magic links.
- The `Behind` namespace logs at `Debug` in the `Development` environment.

## Summary of Requirements

| Requirement | How | Example |
|-------------|-----|---------|
| Self-documenting code | Clear names, small functions | `TryFindUserAsync` instead of `Get` |
| Extension members | C# 14+ `extension(Type)` syntax | `extension(IQueryable<T>)` |
| Immutability | Use `record` and `init` | `public record Email(string Value);` |
| Async I/O | `async/await`, `CancellationToken` | `await repository.FindAsync(..., cancellationToken)` |
| Error handling | Monads (Option, Try, Exceptional, Validation) | `Option<User>` instead of `null` |
| Code placement | Appropriate shared library | Generic in Common, domain in Core.Domain |
| No duplication | Extract to appropriate layer | Base repository in Common.Database |
| SOLID principles | Interface abstractions, single responsibility | Segregated interfaces, dependency injection |
| Clean code | Small functions, meaningful names | Long names > comments |
| Testing | Unit tests for all public functionality | Test file per class in tests folder |
| JSON serialization | TypeDescriptor/JsonConverterFactory | Custom converters in Converters folder |

---

**Remember**: Write code as if the person maintaining it is a violent psychopath who knows where you live. 😉

Follow these standards consistently to maintain a professional, maintainable codebase.
