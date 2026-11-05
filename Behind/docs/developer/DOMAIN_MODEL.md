# Domain Model Design

This document explains the domain model structure, entity design patterns, and domain-driven design principles used in the Behind project.

## Domain-Driven Design (DDD)

Behind follows **Domain-Driven Design** principles with clear boundaries between domain logic and technical concerns.

### Key DDD Concepts

1. **Ubiquitous Language** - Terms shared between domain experts and developers
2. **Bounded Contexts** - Isolated business domains with their own models
3. **Aggregates** - Clusters of related entities with consistency boundaries
4. **Value Objects** - Immutable objects without identity
5. **Entity** - Objects with unique identity
6. **Root** - Entry point to aggregate, manages consistency

## Domain Layer Structure

```
Behind.Core.Domain/
├── User.cs                 # User aggregate root
├── Role.cs                 # Role aggregate root
├── Grant.cs                # Grant value object
├── Email.cs                # Email value object
├── Session.cs              # Session entity
├── AccessToken.cs          # Token value object
├── RefreshToken.cs         # Token value object
├── AuditLogEntry.cs        # Audit value object
├── DomainConstants.cs      # Domain-wide constants
├── UserContext.cs          # Current user context
├── SystemContext.cs        # System execution context
├── Password.cs             # Password value object
├── Site.cs                 # Site version record (VersionedKey id, draft / published)
├── SiteSection.cs          # Site section record (theme, title, content, picture settings) and its theme / picture position enums
├── News.cs                 # News, news category and news comment records
├── Article.cs              # Article and article category records
├── Gallery.cs              # Gallery and gallery item records
├── Carousel.cs             # Site carousel and carousel slide records (CarouselExtensions: interval and link validation)
├── Download.cs             # Download folder and download (uploaded file) records
├── DownloadExtensions.cs   # The display name of a download, defaulting to the file name
├── VfsFile.cs              # Metadata of an uploaded file of the virtual file system
├── VfsFileExtensions.cs    # File name sanitising and the storage key of a file
├── Menu.cs                 # Menu and menu item records, the target and kind enums
├── BuiltInMenus.cs         # The built-in main menu with its tabs and user menu items
├── MenuUrlExtensions.cs    # Which menu item URLs are safe (relative, http, https, mailto, tel)
├── MenuVisibilityExtensions.cs # Role based visibility of menu items
└── Reason.cs              # Business reason value object
```

## Core Aggregates

### 1. User Aggregate

**Aggregate Root**: Abstract record `User`

**Responsibility**: Manage user identity, roles, and permissions.

**Structure**:

```csharp
public abstract record User(Option<IEnumerable<Role>> Roles)
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string ConcurrencyToken { get; init; }

    public bool HasPermission(Grant grant) =>
        Roles.Match(
            () => false,
            roles => roles.Any(r => r.HasPermission(grant))
        );

    public bool HasRole(Role role) =>
        Roles.Match(
            () => false,
            roles => roles.Contains(role)
        );
}
```

**Concrete User Types** (Discriminated Union):

```csharp
public sealed record HumanUser : User
{
    public IEnumerable<Email> Emails { get; init; }
    public string DisplayName { get; init; }

    public HumanUser(
        string displayName,
        IEnumerable<Email> emails,
        Option<IEnumerable<Role>> roles) : base(roles)
    {
        DisplayName = displayName;
        var emailList = emails.ToList();

        if (emailList.Count == 0)
            throw new ArgumentException("HumanUser must have at least one email", nameof(emails));

        Emails = emailList;
    }
}

public sealed record SystemUser(
    Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record GuestUser(
    Option<IEnumerable<Role>> Roles) : User(Roles);

public sealed record BannedUser() : User(None);
```

**Why Multiple Types**:
- Type safety - Compiler prevents invalid operations
- Clear domain semantics - SystemUser vs HumanUser is explicit
- Easy querying - `user is SystemUser` is clear intent
- Pattern matching - Exhaustive type checking

**Operations on User**:

```csharp
extension(User)
{
    public Validation<User> AddRole(Role role)
    {
        return Roles.Match(
            () => Valid(this with { Roles = Some(new[] { role }) }),
            existing => existing.Contains(role)
                ? Invalid(new Error("ROLE_EXISTS", "User already has this role"))
                : Valid(this with { Roles = Some(existing.Append(role)) })
        );
    }

    public Validation<User> RemoveRole(Role role)
    {
        return Roles.Match(
            () => Invalid(new Error("NO_ROLES", "User has no roles")),
            existing => !existing.Contains(role)
                ? Invalid(new Error("ROLE_NOT_FOUND", "User doesn't have this role"))
                : Valid(this with
                {
                    Roles = existing.Count() == 1
                        ? None
                        : Some(existing.Where(r => r != role))
                })
        );
    }

    public Validation<HumanUser> AddEmail(Email email)
    {
        return this is not HumanUser human
            ? Invalid(new Error("NOT_HUMAN", "Only HumanUsers can have emails"))
            : human.Emails.Contains(email)
                ? Invalid(new Error("EMAIL_EXISTS", "Email already assigned"))
                : Valid(human with { Emails = human.Emails.Append(email) });
    }
}
```

### 2. Role Aggregate

**Aggregate Root**: Abstract record `Role`

**Responsibility**: Define user roles and their associated permissions/grants.

**Structure**:

```csharp
public abstract record Role(string Name, Option<IEnumerable<Grant>> Grants)
{
    public Guid Id { get; init; }

    public bool HasPermission(Grant grant) =>
        Grants.Match(
            () => false,
            grants => grants.Contains(grant)
        );
}

public sealed record UserRole(
    string Name,
    Option<IEnumerable<Grant>> Grants) : Role(Name, Grants);

public sealed record SystemRole(
    Option<IEnumerable<Grant>> Grants) : Role(DomainConstants.System, Grants);
```

**Role Hierarchy**:

- **SystemRole** - Special role for system operations
  - Has elevated permissions
  - Cannot be assigned to users normally
  - Reserved for internal use

- **UserRole** - Regular user-assignable roles
  - Admin, Moderator, Member, etc.
  - Composable permissions
  - User-managed

**Operations on Role**:

```csharp
extension(Role)
{
    public Validation<Role> AddGrant(Grant grant)
    {
        return Grants.Match(
            () => Valid(this with { Grants = Some(new[] { grant }) }),
            existing => existing.Contains(grant)
                ? Invalid(new Error("GRANT_EXISTS", "Grant already exists"))
                : Valid(this with { Grants = Some(existing.Append(grant)) })
        );
    }

    public Validation<Role> RemoveGrant(Grant grant)
    {
        return Grants.Match(
            () => Invalid(new Error("NO_GRANTS", "Role has no grants")),
            existing => !existing.Contains(grant)
                ? Invalid(new Error("GRANT_NOT_FOUND", "Grant not assigned"))
                : Valid(this with
                {
                    Grants = existing.Count() == 1
                        ? None
                        : Some(existing.Where(g => g != grant))
                })
        );
    }
}
```

## Value Objects

Value Objects are **immutable objects without identity**. They represent concepts, not things.

### 1. Email

```csharp
public record Email(string Value)
{
    public Email(string value) : this(
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Email required", nameof(value))
            : value.Trim().ToLowerInvariant()
    )
    {
        if (!value.Contains("@"))
            throw new ArgumentException("Invalid email format", nameof(value));
    }

    public override string ToString() => Value;
}
```

**Characteristics**:
- Immutable - Can't change an Email, only create new ones
- No identity - Two Email("user@example.com") are identical
- Validation built-in - Invalid emails can't be created
- Domain semantics - "Email" not just "string"

### 2. Password

```csharp
public record Password(string Hash)
{
    public static Password Create(string plainPassword) =>
        new(BCrypt.HashPassword(plainPassword));

    public bool VerifyHash(string plainPassword) =>
        BCrypt.Verify(plainPassword, Hash);
}
```

**Characteristics**:
- Never stores plain password
- Hash encapsulated
- Verification method built-in
- Can't be "changed" - only replaced

### 3. Grant

```csharp
public record Grant(string Name, Option<string> Description = default);
```

**Domain Grants**:

```csharp
public static class DomainGrants
{
    public static readonly Grant ManageUsers = new("manage_users");
    public static readonly Grant ManageRoles = new("manage_roles");
    public static readonly Grant ModerateContent = new("moderate_content");
    public static readonly Grant ViewAnalytics = new("view_analytics");
}
```

### 3a. Restriction and calculated grants

```csharp
public sealed record Restriction(string Name);

public sealed record UserAccess(
    IReadOnlyList<Role> Roles,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Restrictions);
```

A `Restriction` names a grant that must be taken away. Roles carry grants and restrictions, and users carry
direct grants and restrictions. A user's calculated grants are every role grant plus every direct grant, minus
every role restriction and every direct restriction, so a restriction always wins over a grant.

Built-in role grants are seeded once when the role (or the built-in grant) is first created, so edits made
through the Roles page persist across restarts. Editors can only raise a role or user to grants they hold
themselves, unless they hold `RawDatabase.Manage`.

Endpoints: `GET/POST /roles`, `PUT /roles/{id}/access`, `POST /roles/{id}/reset-grants` (built-in roles only),
`POST /roles/{id}/delete`,
`GET/PUT /users/{id}/access`. `GET /session` returns the calculated grants in `user.grants`.

### 4. Session-Related Value Objects

```csharp
public record AccessToken(string Value, DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

public record RefreshToken(string Value, DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

public record TokenNonce(string Value)
{
    public static TokenNonce Generate() =>
        new(Guid.NewGuid().ToString("N"));
}
```

### 5. Reason

```csharp
public record Reason(string Value)
{
    public Reason(string value) : this(
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Reason required", nameof(value))
            : value.Trim()
    ) { }
}
```

**Usage**: Document "why" for audit trails.

```csharp
public record BanUserCommand(
    Guid UserId,
    Reason Reason);  // Why was user banned?

public record DeleteContentCommand(
    Guid ContentId,
    Reason Reason);  // Why was content deleted?
```

## Entities

Entities are **mutable objects with unique identity**.

### Session Entity

```csharp
public record Session
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public AccessToken AccessToken { get; init; }
    public RefreshToken? RefreshToken { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt;
}

extension(Session)
{
    public Validation<Session> Renew(AccessToken newToken, RefreshToken? newRefreshToken)
    {
        if (!RefreshToken?.IsExpired == true)
            return Invalid(new Error("REFRESH_EXPIRED", "Refresh token expired"));

        return Valid(this with
        {
            AccessToken = newToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = newToken.ExpiresAt
        });
    }

    public Validation<Session> Invalidate()
    {
        return Valid(this with { IsActive = false });
    }
}
```

### Second factor entities

A user can protect the account with a second factor. The records are owned by a user (shadow `Id` and `UserId`), immutable and updated through the immutable repository:

```csharp
public sealed record UserTotp([property: Sensible] string Secret, bool IsConfirmed = false, long LastUsedStep = 0);

public sealed record UserPasskey(string Name, string CredentialId, string PublicKey, long SignCount = 0);

public sealed record UserRecoveryCode([property: Sensible] string Hash);
```

`UserTotp` holds the authenticator app secret (confirmed once a code of it was typed), `UserPasskey` a WebAuthn credential and `UserRecoveryCode` one of the 20 single use recovery codes (a BCrypt hash; see `RecoveryCodeExtensions` in `Behind.Core.Common`). See [Database Architecture](./DATABASE_ARCHITECTURE.md#two-factor-authentication).

## Audit & Compliance

### AuditLogEntry

```csharp
public record AuditLogEntry(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Operation,  // Created, Updated, Deleted, PermissionGranted
    IReadOnlyDictionary<string, string> Changes,  // Before/After values
    Option<Guid> UserId,  // Who made the change?
    DateTime Timestamp)
{
    public AuditLogEntry(
        string entityName,
        Guid entityId,
        string operation,
        IReadOnlyDictionary<string, string> changes,
        Option<Guid> userId = default) : this(
        Id: Guid.NewGuid(),
        EntityName: entityName,
        EntityId: entityId,
        Operation: operation,
        Changes: changes,
        UserId: userId,
        Timestamp: DateTime.UtcNow)
    { }
}
```

**Usage**:

```csharp
var audit = new AuditLogEntry(
    EntityName: "User",
    EntityId: user.Id,
    Operation: "Created",
    Changes: new Dictionary<string, string>
    {
        ["Email"] = newUser.Email.Value,
        ["DisplayName"] = newUser.DisplayName
    },
    UserId: currentUser.Map(u => u.Id)
);

await auditRepository.AddAsync(audit);
```

### WallOfShameEntry

```csharp
public record WallOfShameEntry(
    Guid Id,
    Guid UserId,
    string Reason,
    DateTime CreatedAt);

extension(User)
{
    public Validation<WallOfShameEntry> Ban(Reason reason, Option<Guid> bannedById)
    {
        if (this is BannedUser)
            return Invalid(new Error("ALREADY_BANNED", "User already banned"));

        return Valid(new WallOfShameEntry(
            Id: Guid.NewGuid(),
            UserId: this.Id,
            Reason: reason.Value,
            CreatedAt: DateTime.UtcNow
        ));
    }
}
```

## Domain Events

Domain events represent something significant that happened in the domain.

```csharp
public abstract record DomainEvent
{
    public Guid EventId { get; init; }
    public DateTime OccurredAt { get; init; }
    public Option<Guid> UserId { get; init; }
}

public record UserCreatedEvent(
    Guid UserId,
    Email Email,
    Option<Guid> CreatedById = default) : DomainEvent;

public record UserBannedEvent(
    Guid UserId,
    Reason Reason,
    Option<Guid> BannedById = default) : DomainEvent;

public record RoleGrantedEvent(
    Guid UserId,
    Guid RoleId,
    Option<Guid> GrantedById = default) : DomainEvent;

public record PermissionGrantedEvent(
    Guid RoleId,
    Grant Grant,
    Option<Guid> GrantedById = default) : DomainEvent;
```

**Usage**:

```csharp
public class UserService
{
    private readonly IEventPublisher eventPublisher;
    private readonly List<DomainEvent> events = new();

    public async Task<User> CreateUserAsync(CreateUserDto dto)
    {
        var user = new HumanUser(dto.DisplayName, new[] { new Email(dto.Email) }, None);

        // Record event
        events.Add(new UserCreatedEvent(
            UserId: user.Id,
            Email: user.Emails.First(),
            CreatedById: currentUser.Map(u => u.Id)
        ));

        await repository.AddAsync(user);

        // Publish events for other services
        foreach (var @event in events)
        {
            await eventPublisher.PublishAsync(@event);
        }

        return user;
    }
}
```

## Domain Constants

Centralize domain-wide constants:

```csharp
public static class DomainConstants
{
    // System roles
    public const string System = "System";
    public const string AdminRole = "Admin";
    public const string ModeratorRole = "Moderator";
    public const string MemberRole = "Member";

    // Grant names
    public const string ManageUsersGrant = "manage_users";
    public const string ManageRolesGrant = "manage_roles";
    public const string ModerateContentGrant = "moderate_content";

    // Default IDs
    public static readonly Guid DefaultRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    // Limits
    public const int MaxEmailsPerUser = 5;
    public const int MaxPasswordLength = 128;
    public const int MinPasswordLength = 8;
    public const int SessionTimeoutMinutes = 30;
}
```

## Validation in Domain

Domain validation is about enforcing business rules, not syntax.

```csharp
public static class DomainValidation
{
    public static Validation<HumanUser> ValidateNewUser(
        string displayName,
        Email email,
        string plainPassword)
    {
        return (
            from displayNameValid in ValidateDisplayName(displayName)
            from passwordValid in ValidatePassword(plainPassword)
            select new HumanUser(displayNameValid, new[] { email }, None)
        );
    }

    private static Validation<string> ValidateDisplayName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? Invalid(new Error("DISPLAY_NAME_REQUIRED", "Display name is required"))
            : displayName.Length > 100
                ? Invalid(new Error("DISPLAY_NAME_TOO_LONG", "Display name too long"))
                : Valid(displayName.Trim());
    }

    private static Validation<Password> ValidatePassword(string plainPassword)
    {
        var errors = new List<Error>();

        if (plainPassword.Length < DomainConstants.MinPasswordLength)
            errors.Add(new Error("PASSWORD_TOO_SHORT", "Password must be at least 8 characters"));

        if (!plainPassword.Any(char.IsUpper))
            errors.Add(new Error("PASSWORD_NO_UPPER", "Password must contain uppercase letter"));

        if (!plainPassword.Any(char.IsDigit))
            errors.Add(new Error("PASSWORD_NO_DIGIT", "Password must contain digit"));

        return errors.Count > 0
            ? Invalid(errors)
            : Valid(Password.Create(plainPassword));
    }
}
```

## Entity Configuration (Database Mapping)

Value objects and entities are configured in Behind.Core.Database:

```csharp
extension(ModelBuilder)
{
    public ModelBuilder ConfigureUserAggregate(this ModelBuilder modelBuilder)
    {
        var userBuilder = modelBuilder.Entity<User>();

        userBuilder.HasDiscriminator<string>("UserType")
            .HasValue<HumanUser>(nameof(HumanUser))
            .HasValue<SystemUser>(nameof(SystemUser))
            .HasValue<GuestUser>(nameof(GuestUser))
            .HasValue<BannedUser>(nameof(BannedUser));

        userBuilder.OwnsMany(u => (u as HumanUser)!.Emails, emailBuilder =>
        {
            emailBuilder.ToJson();
            emailBuilder.HasKey("Id");
            emailBuilder.Property(e => e.Value).HasMaxLength(255);
        });

        userBuilder.HasMany(u => u.Roles)
            .WithMany()
            .UsingEntity("UserRoles");

        return modelBuilder;
    }
}
```

## Inheritance vs Composition

### Use Inheritance When:
- Creating discriminated unions (User types)
- Maintaining polymorphic behavior
- Database-level inheritance makes sense

### Use Composition When:
- Combining behaviors (Session with Token)
- Avoiding deep hierarchies
- Flexibility over inheritance

**Example - Session Composition**:

```csharp
public record Session(
    Guid Id,
    Guid UserId,
    AccessToken Token,
    Option<RefreshToken> RefreshToken,
    DateTime CreatedAt);

extension(Session)
{
    public bool IsTokenExpired(this Session session) =>
        session.Token.IsExpired;

    public Validation<Session> RenewToken(AccessToken newToken)
    {
        return session.RefreshToken.Match(
            () => Invalid(new Error("NO_REFRESH", "No refresh token")),
            refresh => refresh.IsExpired
                ? Invalid(new Error("REFRESH_EXPIRED", "Refresh token expired"))
                : Valid(session with { Token = newToken })
        );
    }
}
```

## Domain Modeling Best Practices

✓ **DO**:
- Make invalid states impossible
- Encapsulate validation in aggregates
- Use value objects for domain concepts
- Validate at domain boundaries
- Use discriminated unions for type safety
- Document domain concepts with clear names

✗ **DON'T**:
- Use generic `string`/`int` for domain concepts
- Allow invalid aggregates to be created
- Expose implementation details
- Skip validation at boundaries
- Mix domain logic with persistence
- Create "god" objects with too much responsibility

## Summary

| Concept | Purpose | Example |
|---------|---------|---------|
| Aggregate | Consistency boundary | User aggregate |
| Entity | Mutable with identity | Session |
| Value Object | Immutable without identity | Email, Password |
| Domain Event | Something significant happened | UserBannedEvent |
| Domain Rule | Business logic constraint | User must have email |
| Specification | Query encapsulation | UserSpecifications.Active |
| Context | Bounded context | Behind.Core.Domain |

---

**Next Steps**:
- Review DATABASE_ARCHITECTURE.md for persistence mapping
- Check WEBAPI_CONVENTIONS.md for API representation
- See FUNCTIONAL_PROGRAMMING.md for result handling
