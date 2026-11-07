# Project Overview

Behind is a modern .NET 10 application built with clean architecture principles, functional programming paradigms, and emphasis on code quality and maintainability.

## Project Purpose

Behind is designed as a scalable, maintainable backend system using microservices architecture (via .NET Aspire) with:
- Strong domain-driven design
- Functional programming patterns
- Type-safe error handling
- Comprehensive API documentation
- Production-ready observability

## Technology Stack

### Core Framework
- **Runtime**: .NET 10
- **Web Framework**: ASP.NET Core 10.0.8
- **Language Features**: C# 12 (records, extension types, top-level statements)

### Data Access
- **ORM**: Entity Framework Core 10.0.10
- **Database Support**:
  - SQLite 3.53.3 (SQLitePCLRaw 2.1.12)
  - PostgreSQL (Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3)
- **Migration**: EF Core Code Migrations via Behind.Core.Database.Migrator

### Security & Authentication
- **Authentication**: JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer)
- **Password Hashing**: BCrypt.Net-Next 4.2.0
- **Two-Factor Authentication**: Otp.NET 1.4.1 (TOTP support)

### Observability & Monitoring
- **Tracing & Metrics**: OpenTelemetry 1.16.0
- **Instrumentation**: 
  - ASP.NET Core
  - HTTP Client
  - Runtime
- **Export**: OTLP (OpenTelemetry Protocol) compatible exporters

### API Documentation
- **OpenAPI**: Microsoft.AspNetCore.OpenApi 10.0.10
- **UI**: Scalar.AspNetCore 2.14.11 (replaces Swagger)
- **Security**: Bearer token documentation

### Testing
- **Framework**: xUnit 2.9.2
- **Mocking**: NSubstitute 5.3.0
- **Coverage**: Coverlet 6.0.2

### Utilities
- **CLI Tools**: System.CommandLine 2.0.7
- **Dynamic Queries**: System.Linq.Dynamic.Core 1.7.3
- **Serialization**: System.Text.Json with TypeDescriptor factories

### Orchestration
- **Service Hosting**: Aspire 13.4.6
- **Containers**: Docker support via Aspire
- **Data Services**: PostgreSQL, Valkey (Redis alternative)

## Project Structure

### /src/Aspire
.NET Aspire orchestration project for service composition and local development setup.

**Key Projects:**
- `Behind.AppHost` - Service orchestration and configuration

### /src/Common
Reusable libraries shared across all projects.

**Projects:**
- **Behind.Common** (Core foundation)
  - Functional programming utilities (Option, Try, Exceptional, Validation)
  - Extension methods and functional combinators
  - Filter and query abstractions
  - JSON serialization converters
  - Configuration extensions
  - Common value objects and abstractions

- **Behind.Common.Database** (Data layer abstractions)
  - Repository pattern interfaces (IRepository, IImmutableEntityRepository)
  - Unit of Work pattern
  - DbContext extensions
  - Filter application to IQueryable

- **Behind.Common.WebApi** (API layer utilities)
  - WebApplication builder extensions
  - OpenAPI/OpenTelemetry configuration
  - Global exception handlers
  - API filters and middleware
  - Configuration serialization

### /src/Core
Core business logic and domain implementation.

**Projects:**
- **Behind.Core.Common**
  - Core domain utilities
  - Email value object
  - Password hashing abstractions
  - TOTP configuration
  - Converters for domain types

- **Behind.Core.Domain** (Domain models - DDD)
  - User aggregate (HumanUser, SystemUser, GuestUser, BannedUser)
  - Role aggregate (UserRole, SystemRole)
  - Grant and Permission models
  - Session and Token models
  - Email value object
  - Audit log entries
  - Wall of Shame entries
  - Sites (versioned with `VersionedKey` and `ActiveVersion`) and the uploaded files of the virtual file system
  - News with categories (header image) and comments
  - Articles with their own categories (header image), searchable by title and content
  - Galleries of images with optional descriptions with server side thumbnails, drafts and publishing
  - Downloads: a tree of folders (slug and display name) with the uploaded files (display name, size, uploaded at and by) of the virtual file system
  - Menus with menu items (sub items, target, user menu items, visibility by role) and the built-in main menu
  - Route aliases: short routes (`#/manuals`) that open a longer page of the application (`#/downloads/products/manuals`)
  - Access settings: a restriction page that shows the website only to users with a chosen role (with a custom message), and sign-up moderation (new accounts wait for approval before they can sign in)

- **Behind.Core.Database** (Data implementation)
  - DbContext configuration
  - Entity type configurations
  - Repository implementations
  - Query specifications

- **Behind.Core.Database.Migrator**
  - EF Core migrations tooling
  - Database initialization

- **Behind.Core.Database.Sqlite**
  - SQLite-specific configurations

- **Behind.Core.Database.Postgres**
  - PostgreSQL-specific configurations (`CoreContextPostgres`, `VersioningContextPostgres`)
  - Its own migrations, starting from one `Initial` migration per context

- **Behind.Core.WebApi** (API endpoints)
  - API controllers
  - Request/response models
  - WebApi-specific configurations

- **Behind.Core.Tools**
  - CLI tools and utilities
  - `dataprotection create`: creates the data protection certificate (random or prompted password) and a `security.json` with the `Security:DataProtection` section
  - `startup create`: generates `security.json` (data protection certificate and keys path, TOTP secret key, JWT signing key, developer elevation TOTP key, magic link secret), `database.json` (SQLite), `backend.json` and `frontend.json` (site title, default locale and sites settings, so the Settings page can edit them). Options: `--data-directory` (default `data`), `--config-directory` (default `.`), `--issuer`, `--audience`, `--email-confirmation`, `--prompt`, `--force`, `--skip-existing`. Exits with a non-zero code on failure
  - Runs as a container too (`tools/publish-core-tools-container.sh` publishes `reverse-project/behind/core-tools`); the paths written to the configuration files are absolute, so mount the data directory where the other containers see it:

    ```bash
    podman run --rm --user 0:0 \
        -v ./data:/app/data:Z -v ./config:/app/config:Z \
        git.prod.zwebnt.hu/reverse-project/behind/core-tools:latest \
        startup create --data-directory /app/data --config-directory /app/config --skip-existing
    ```

    The generated authenticator URIs are printed once; the image runs as user `1654` by default, so either make the mounted directories writable by it or pass `--user` as above.

### /src/Web
Web application (frontend or web-specific backend features).

**Projects:**
- `Behind.Web` - Web-specific functionality

### /tests
Test projects using xUnit.

**Projects:**
- **Behind.Tests**
  - Unit tests for all functionalities
  - Test helpers and fixtures
  - Mocking utilities

## Architecture Layers

### 1. Common Layer (/src/Common)
**Purpose**: Provide reusable, generic utilities and abstractions

**Responsibilities**:
- Functional programming patterns (monads)
- Generic database abstractions
- Generic WebApi utilities
- Serialization converters
- Filter abstractions

**Examples**: Option<T>, Try<T>, IRepository<T>

### 2. Core Layer (/src/Core)
**Purpose**: Implement domain-specific business logic

**Responsibilities**:
- Domain models and aggregates
- Domain rules and validation
- Data persistence (DbContext, repositories)
- Core API controllers
- Domain-specific utilities

**Examples**: User, Role, Grant aggregates; permission validation

### 3. Web Layer (/src/Web)
**Purpose**: Handle web-specific concerns

**Responsibilities**:
- Web application features
- Web-specific middleware
- Web-specific controllers or endpoints

## Key Design Patterns

### 1. Monadic Composition
Uses functional monads for error handling and optional values:
- `Option<T>` - Nullable replacement with Railway-Oriented Programming
- `Try<T>` - Exception handling without throws
- `Exceptional<T>` - Exception vs Success result type
- `Validation<T>` - Accumulating error validation

### 2. Railway-Oriented Programming
Functions return Result types instead of throwing exceptions, enabling composition:
```csharp
public Option<User> FindUser(Guid id)  // None if not found
public Exceptional<Auth> Authenticate(Credentials)  // Exception or Auth
public Validation<Command> ValidateCommand(Dto)  // Many errors or valid command
```

### 3. Extension Methods (C# 12 `extension` syntax)
All extension methods use modern C# syntax in `*Extensions.cs` files:
```csharp
extension(Type)
{
    public new method(...) { ... }
}
```

### 4. Repository Pattern
Generic `IRepository<T>` and `IRepository<T, TKey>` interfaces provide:
- Find, Add, Update, Delete operations
- Async/await support with CancellationToken
- Option<T> return types instead of nulls

### 5. Filter/Query Specification
`Filter<T>` records encapsulate query predicates for composable filtering.

### 6. Immutable Records
Domain objects use `record` types for:
- Value semantics
- Immutability by default
- ToString implementation
- Structural equality

## Technology Decisions

| Decision | Rationale |
|----------|-----------|
| Functional Programming | Type-safe error handling, composable, testable |
| Records over Classes | Immutability, equality, conciseness |
| Monads (Option/Try/etc) | Eliminate nulls and exception control flow |
| Extension Methods | Non-invasive utility additions, fluent APIs |
| EF Core | LINQ support, async/await, migrations, database agnostic |
| JWT Authentication | Stateless, distributed system friendly |
| OpenTelemetry | Vendor-neutral observability, standards-based |
| xUnit + NSubstitute | Modern testing, clean isolation |

## Build & Deployment

### Local Development
- Dev container with .NET 10 SDK pre-configured
- Aspire orchestration for service coordination
- SQLite (or PostgreSQL) for the local database
- Docker compose via Aspire

### Development Environment
- Configuration via `appsettings.json` and `appsettings.Development.json`
- OpenTelemetry configured per environment
- Database migrations via `Behind.Core.Database.Migrator`

### Production Deployment
- Docker containers orchestrated by Aspire
- PostgreSQL for persistent data
- OTLP exporter for centralized observability
- JWT authentication required

## Dependency Management

- **Centralized versioning** via `Directory.Packages.props`
- **Transitive pinning** enabled for reproducible builds
- **Regular updates** to keep dependencies current
- **Security scanning** for vulnerabilities (see LIMITATIONS_AND_ISSUES.md)

## Quality Assurance

### Code Standards
- **No comments** - Code must be self-documenting
- **Clean Code** - Meaningful names, small functions, single responsibility
- **SOLID Principles** - Especially Dependency Inversion, Interface Segregation
- **DRY** - No code duplication; shared logic in Common

### Testing
- **Unit Tests Required** - All public functionality must have tests
- **Framework**: xUnit with NSubstitute
- **Test Location**: `/tests/Behind.Tests/` organized by module

### Documentation
- **Code Documentation** - Self-documenting via clear names and types
- **Architectural Documentation** - This folder contains guides and patterns
- **API Documentation** - OpenAPI/Swagger in app via Scalar

## Performance & Scalability

### Async All The Way
- All I/O operations use async/await
- Support for CancellationToken throughout
- Thread-safe patterns

### Scalability
- Stateless API design (JWT)
- Repository pattern enables easy data source changes
- Aspire orchestration supports horizontal scaling
- OpenTelemetry for distributed tracing

## Security

### Built-In
- JWT Bearer authentication
- BCrypt password hashing
- TOTP two-factor authentication
- Role-based access control (RBAC)
- Grant-based permissions
- Email confirmation at sign-up: `Security:EmailConfirmation` (default `true`) sends a confirmation link; when `false`, the new email's `ConfirmedAt` is set to the sign-up time; `POST /sign/up/confirm/resend` sends a new link (404 when confirmation is off, 204 whether or not the account exists, rate limited by `ConfirmationEmailExtensions.ResendRateLimitPolicy`)
- Captcha on the sign in and sign up pages: `GET /captcha` (rate limited by `CaptchaExtensions.RateLimitPolicy`) returns `{ id, image }` (PNG data URI rendered by `CaptchaImageExtensions` in `Behind.Common`); the `id` is an AES-GCM sealed ticket (expiry and answer) that only the server can open, so no challenge is stored. `POST /sign/in/password`, `/sign/in/magiclink`, `/sign/up/password` and `/sign/up/confirm/resend` require `captcha: { id, answer }` through `RequireCaptcha<TRequest>()` (the request implements `ICaptchaRequest`); `CaptchaService` accepts a challenge once, until it expires (5 minutes), and answers a rejection with 400 `{ code: "captchaInvalid" }`
- Two-factor authentication (TOTP with a QR code, passkeys, recovery codes): optional per user, required for everybody with `Security:TwoFactorRequired` (default `false`, in `security.json`). Services live in `Behind.Core.WebApi/TwoFactor`: `TwoFactorGate` decides after the password or magic link (and after the email confirmation and ban checks, before any session exists) whether `SignInEndpoints.StartSession` answers with a `TwoFactorChallengeResponse` `{ ticket, methods, setupRequired, function }` instead of the tokens; `TwoFactorTickets` seals the ticket (user, stage Verify/Setup/Profile, expiry 5 minutes, optional WebAuthn ceremony) with ASP.NET Core Data Protection; `TwoFactorVerification` checks authenticator codes (SHA-1, six digits, one 30 second step of drift, a step is accepted once through `UserTotp.LastUsedStep`), recovery codes (`RecoveryCodes`: 20 codes of 8 characters from `A-Z0-9`, stored as BCrypt hashes, a used code is deleted, the last one triggers a fresh bunch) and passkeys (`PasskeyCeremonies` wraps the `Fido2` package; relying party id and origin come from `Frontend:Url` or the request address; the sign count is stored); `TwoFactorEnrollment` starts and confirms the authenticator app (`Totp.Generate`, `ToOtpUri`, `ToQrCodeSvg`), registers and removes passkeys and refuses to remove the last method while it is required; `TwoFactorSignIn` finishes the session. Endpoints: `TwoFactorSignInEndpoints` (`/sign/in/two-factor/...`, ticket based, rate limited by `TwoFactorExtensions.RateLimitPolicy`) and `TwoFactorProfileEndpoints` (`/profile/two-factor/...`, signed in, not for impersonation sessions). Wrong codes answer 403 `{ code: "invalidCode" }`, a bad or expired ticket 401 `{ code: "ticketInvalid" }`; `SignInResponse.RecoveryCodes` carries newly generated codes

### Best Practices
- Sensitive field masking in logs
- Secure configuration handling
- No hardcoded secrets
- Input validation via Validation<T>

## Observability

### Tracing
- Distributed tracing via OpenTelemetry
- ASP.NET Core instrumentation
- HTTP client instrumentation
- OTLP exporter support

### Metrics
- Runtime metrics
- HTTP request metrics
- ASP.NET Core metrics

### Logging
- Structured logging with OpenTelemetry
- Sensitive field masking
- Environment-specific configuration

## Current Status

### Latest Versions
- .NET Framework: 10.0.10 (ASP.NET Core, EF Core, Extensions)
- OpenTelemetry: 1.16.0
- JWT: 10.0.8

### Known Limitations
- See [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) for dependency vulnerabilities
- Some transitive dependencies have unpatched CVEs

---

**Next Steps**:
- Review [CODING_STANDARDS.md](./CODING_STANDARDS.md) for mandatory practices
- Study [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) for pattern understanding
- Check [DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md) for architectural decisions
