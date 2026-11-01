# Design Patterns and Architectural Decisions

This document explains the architectural patterns, design decisions, and their rationales in the Behind codebase.

## Layered Architecture

Behind follows a **clean layered architecture** with clear separation of concerns:

```
┌─────────────────────────────────┐
│   Web/API Layer                 │ (Behind.Web, Behind.Core.WebApi)
│   - Controllers                 │ - HTTP endpoints
│   - Request/Response DTOs       │ - Route handlers
│   - API middleware              │ - CORS, authentication
├─────────────────────────────────┤
│   Domain Layer                  │ (Behind.Core.Domain)
│   - Domain models (records)     │ - Business rules
│   - Aggregates                  │ - Value objects
│   - Domain events               │ - Domain logic
├─────────────────────────────────┤
│   Application Layer             │ (Behind.Core.Database)
│   - DbContext                   │ - Use cases
│   - Entity configurations       │ - Application logic
│   - Repository implementations  │ - Data loading
├─────────────────────────────────┤
│   Infrastructure Layer          │ (Behind.Common.*)
│   - Database abstractions       │ - Generic patterns
│   - API utilities               │ - Configuration
│   - Functional utilities        │ - Serialization
└─────────────────────────────────┘
```

### Layer Responsibilities

#### 1. Web/API Layer (Behind.Core.WebApi, Behind.Web)
**Responsibilities**:
- Handle HTTP requests/responses
- Validate user input (basic syntax checks)
- Call application services
- Format responses for clients
- Implement authentication/authorization

**Rules**:
- No business logic
- No direct database queries
- Depends on domain and application layers
- Always use DTOs for input/output

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

#### 2. Domain Layer (Behind.Core.Domain)
**Responsibilities**:
- Domain models and aggregates
- Business rules and validation
- Domain events
- Value objects

**Rules**:
- Independent of infrastructure
- No database knowledge
- Express business logic clearly
- Immutable records

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

#### 3. Application Layer (Behind.Core.Database)
**Responsibilities**:
- DbContext configuration
- Entity configurations
- Repository implementations
- Use case orchestration
- Data loading and persistence

**Rules**:
- Implements domain interfaces
- Uses Entity Framework Core
- Knows about persistence mechanisms
- Orchestrates data operations

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

#### 4. Infrastructure Layer (Behind.Common.*)
**Responsibilities**:
- Generic, reusable abstractions
- Serialization/deserialization
- Configuration handling
- Logging and observability setup
- Framework utilities

**Rules**:
- Must be generic and reusable
- No domain-specific logic
- No business logic
- Single responsibility

```csharp
// Generic repository interface (no domain knowledge)
public interface IRepository<TAggregate, TKey>
{
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}

// Generic filter expression
public record Filter<T>(Expression<Func<T, bool>> Predicate)
{
    public IQueryable<T> Apply(IQueryable<T> queryable) => queryable.Where(Predicate);
}

// Generic JSON converter
public class OptionJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType &&
           typeToConvert.GetGenericTypeDefinition() == typeof(Option<>);
}
```

## Design Patterns

### 1. Repository Pattern

**Purpose**: Decouple domain from data access; provide data access abstraction.

**Structure**:
```csharp
// Interface in Behind.Common.Database (generic)
public interface IRepository<TAggregate, TKey>
{
    TKey GetKey(TAggregate aggregate);
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    void Update(TAggregate aggregate);
    void Delete(TAggregate aggregate);
}

public interface IRepository<T> : IRepository<T, Guid> where T : class;

// Implementation in Behind.Core.Database (domain-specific)
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

**Benefits**:
- Testable (mock repository easily)
- Swappable data sources
- Centralized query logic
- Consistent data access patterns

### 2. Unit of Work Pattern

**Purpose**: Coordinate multiple repositories as a single transaction.

**Interface**:
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

**Usage**:
```csharp
public async Task<Exceptional<User>> RegisterUserAsync(RegisterDto dto)
{
    var user = new User(dto);
    var defaultRole = await uow.Roles.TryFindAsync(Guid.Parse(DomainConstants.DefaultRoleId));
    
    user.AssignRole(defaultRole);
    
    await uow.Users.AddAsync(user);
    await uow.CommitAsync();  // All repositories commit together
    
    return user;
}
```

**Benefits**:
- ACID transactions
- Consistency across repositories
- Rollback capability
- Clean separation of concerns

### 3. Aggregate Pattern (Domain-Driven Design)

**Purpose**: Group related entities and value objects into coherent aggregates with clear boundaries.

**Definition**: An aggregate is a cluster of associated objects that are treated as a single unit for the purpose of data consistency.

**Structure**:
```csharp
// Aggregate Root
public abstract record User(Option<IEnumerable<Role>> Roles)
{
    public Guid Id { get; init; }
    
    // Business operations on the aggregate
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

// Child entities within aggregate
public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }  // Value objects
    public string DisplayName { get; init; }
}

// Value objects
public record Email(string Value);
public record Role(string Name, Option<IEnumerable<Grant>> Grants);
public record Grant(string Name);
```

**Rules**:
- Access only through aggregate root
- Single responsibility per aggregate
- Immutable within aggregate
- Transactional consistency boundary

**Example - Aggregate in Repository**:
```csharp
public class UserRepository : IRepository<User>
{
    public async Task<Option<User>> TryFindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbSet
            .Include(u => u.Roles)        // Load related Role entities
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        
        return user;  // Return entire aggregate or None
    }
    
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await dbSet.AddAsync(user, cancellationToken);
        // All related entities added automatically
    }
}
```

### 4. Specification Pattern

**Purpose**: Encapsulate query logic in reusable, testable objects.

**Implementation via Filters**:
```csharp
// Generic filter
public record Filter<T>(Expression<Func<T, bool>> Predicate)
{
    public IQueryable<T> Apply(IQueryable<T> queryable) => queryable.Where(Predicate);
}

// Specific specifications
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

// Usage
var activeUsersWithAdminRole = await dbSet
    .Where(UserSpecifications.Active.Predicate)
    .Where(UserSpecifications.WithRole(adminRole).Predicate)
    .ToListAsync();
```

**Benefits**:
- Reusable query logic
- Tested separately from repositories
- More readable queries
- Single place to update common queries

### 5. Dependency Injection

**Purpose**: Decouple dependencies and enable composition at runtime.

**Configuration** (in Behind.Common.WebApi):
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

**Usage** (in controller):
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

**Benefits**:
- Loose coupling
- Testable with fake implementations
- Configurable at startup
- Follows Dependency Inversion

### 6. Decorator Pattern

**Purpose**: Add behavior to objects dynamically without modifying their structure.

**Example** (Repository with logging):
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
    
    // ... other methods
}

// Registration
services.AddScoped<IRepository<User>, UserRepository>();
services.Decorate<IRepository<User>, LoggingUserRepository>();
```

### 7. Factory Pattern

**Purpose**: Encapsulate complex object creation logic.

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

**Usage**:
```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new OptionJsonConverterFactory());
options.Converters.Add(new ExceptionalJsonConverterFactory());

var json = JsonSerializer.Serialize(userWithOptionalFields, options);
```

## Architectural Patterns

### 1. Railway-Oriented Programming (ROP)

Already detailed in FUNCTIONAL_PROGRAMMING.md. Used throughout for error handling.

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

**Separation of read and write models**:

```csharp
// Command (write operation)
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

// Query (read operation)
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

### 3. Service Locator (Anti-pattern - Avoid)

**Don't use this**:
```csharp
// ❌ BAD: Creates hidden dependencies
public class UserService
{
    public async Task<User> GetUserAsync(Guid id)
    {
        var repository = ServiceLocator.GetService<IRepository<User>>();
        return await repository.FindAsync(id);
    }
}
```

**Use dependency injection instead**:
```csharp
// ✓ GOOD: Explicit dependencies
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

## Data Flow

### Complete Request Flow

```
HTTP Request
    ↓
[API Controller/Endpoint]
    ↓ (Validate input syntax)
[Application Service]
    ↓ (Validate business rules)
[Domain Logic/Aggregates]
    ↓ (Execute business operation)
[Repository/Data Access]
    ↓ (Persist to database)
[Entity Framework Core]
    ↓
[Database]
    ↓ (Return result)
[Exceptional<T> / Validation<T>]
    ↓ (Map to response DTO)
[API Response]
```

### Example: User Registration

```csharp
// 1. HTTP Request arrives
[HttpPost("register")]
public async Task<IActionResult> RegisterAsync(RegisterDto dto)
{
    // 2. Controller validates syntax and calls service
    var result = await userService.RegisterAsync(dto);
    
    // 3. Service validates business rules
    // 4. Domain creates aggregate
    // 5. Repository persists
    
    return result.Match(
        errors => BadRequest(new { errors }),
        user => CreatedAtAction(nameof(GetUser), new { id = user.Id })
    );
}

// Application Service
public class UserService
{
    public async Task<Validation<User>> RegisterAsync(RegisterDto dto)
    {
        // Validate: Email unique
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

## Design Principles Applied

### DRY (Don't Repeat Yourself)
- Common code in Behind.Common
- Base repository in Behind.Common.Database
- Shared extensions across projects

### KISS (Keep It Simple, Stupid)
- Small, focused functions
- Clear data flow
- Minimal abstractions

### SOLID

1. **Single Responsibility** - Each class has one reason to change
2. **Open/Closed** - Extend through interfaces, not modification
3. **Liskov Substitution** - Subtypes substitute for base types
4. **Interface Segregation** - Clients don't depend on unused methods
5. **Dependency Inversion** - Depend on abstractions, not concretions

### Clean Code
- Self-documenting code
- Meaningful names
- No comments needed
- Small functions

## Decision Matrix

| Scenario | Pattern | Example |
|----------|---------|---------|
| Find optional value | Option<T> + Repository | `TryFindAsync(id)` |
| Handle exceptions | Try<T> or Exceptional<T> | `ParseJson<T>(json)` |
| Validate input | Validation<T> | `ValidateRegistration(dto)` |
| Compose operations | Bind/SelectMany | Chain monad operations |
| Encapsulate queries | Filter/Specification | UserSpecifications.ByEmail |
| Manage consistency | Aggregate | User aggregate with Roles |
| Coordinate repos | Unit of Work | Multi-repo transactions |

---

**Next Steps**:
- Review actual implementations in codebase
- Check DATABASE_ARCHITECTURE.md for data layer details
- See WEBAPI_CONVENTIONS.md for API-specific patterns
