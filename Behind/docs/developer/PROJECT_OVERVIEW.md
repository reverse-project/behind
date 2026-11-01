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
  - PostgreSQL (via Aspire)
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
  - Sites (versioned with `VersionedKey` and `ActiveVersion`) and site images

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

- **Behind.Core.WebApi** (API endpoints)
  - API controllers
  - Request/response models
  - WebApi-specific configurations

- **Behind.Core.Tools**
  - CLI tools and utilities
  - `dataprotection create`: creates the data protection certificate (random or prompted password) and a `security.json` with the `Security:DataProtection` section

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
- SQLite for local database
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
- Email confirmation at sign-up: `Security:EmailConfirmation` (default `true`) sends a confirmation link; when `false`, the new email's `ConfirmedAt` is set to the sign-up time

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
