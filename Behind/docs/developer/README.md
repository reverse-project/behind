# Behind Codebase Documentation

Comprehensive documentation for the Behind project - a modern, clean architecture .NET application following functional programming paradigms, SOLID principles, and Clean Code practices.

## 📚 Documentation Index

### Core Documentation
- **[PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md)** - Project structure, technology stack, and overall architecture
- **[DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md)** - Architectural patterns and design decisions
- **[CODING_STANDARDS.md](./CODING_STANDARDS.md)** - Mandatory coding standards, conventions, and best practices
- **[FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md)** - Functional programming patterns and monads used throughout the codebase

### Architecture Guides
- **[DATABASE_ARCHITECTURE.md](./DATABASE_ARCHITECTURE.md)** - Database layer, Entity Framework patterns, and repository design
- **[WEBAPI_CONVENTIONS.md](./WEBAPI_CONVENTIONS.md)** - Web API configuration, middleware, and OpenAPI documentation
- **[DOMAIN_MODEL.md](./DOMAIN_MODEL.md)** - Domain models, aggregates, and entity design
- **[EMAIL_SENDER_IMPLEMENTATION.md](./EMAIL_SENDER_IMPLEMENTATION.md)** - Email sender configuration, implementation details, and usage guide

### Operational Guides
- **[LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md)** - Known limitations, current issues, and dependency vulnerabilities
- **[GETTING_STARTED.md](./GETTING_STARTED.md)** - Setup, build, run, and development workflow

## 🎯 Quick Navigation

**New to the project?** Start with [PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md)

**Want to understand the code patterns?** Read [CODING_STANDARDS.md](./CODING_STANDARDS.md) and [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md)

**Need to work with data?** See [DATABASE_ARCHITECTURE.md](./DATABASE_ARCHITECTURE.md)

**Building APIs?** Check [WEBAPI_CONVENTIONS.md](./WEBAPI_CONVENTIONS.md)

**Aware of current issues?** Review [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md)

## 🏗️ Project Structure

```
Behind/
├── src/
│   ├── Aspire/              # .NET Aspire orchestration
│   ├── Common/              # Shared libraries
│   │   ├── Behind.Common    # Core functional patterns and utilities
│   │   ├── Behind.Common.Database    # Database abstractions
│   │   └── Behind.Common.WebApi      # WebApi utilities
│   ├── Core/                # Core business logic
│   │   ├── Behind.Core.Common        # Core domain utilities
│   │   ├── Behind.Core.Domain        # Domain models and entities
│   │   ├── Behind.Core.Database      # Database implementations
│   │   ├── Behind.Core.Database.Migrator # EF Migrations
│   │   ├── Behind.Core.Tools         # CLI tools
│   │   └── Behind.Core.WebApi        # Core API controllers
│   └── Web/                 # Web application
├── tests/                   # Test projects
└── docs/
    ├── developer/           # This documentation
    └── user-manual/         # End user manuals

```

## 🔑 Key Principles

The Behind codebase is built on these fundamental principles:

1. **Functional Programming First** - Heavy use of monads (Option, Try, Exceptional, Validation), immutable data structures, and pure functions
2. **Clean Architecture** - Clear separation of concerns with dedicated layers for Common, Core Domain, and Core API
3. **DRY (Don't Repeat Yourself)** - Shared code in Common and Core.Common libraries
4. **SOLID Principles** - Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion
5. **Clean Code** - Self-documenting code; no unnecessary comments
6. **Type Safety** - Strong typing with records and immutable objects
7. **Thread Safety** - Async/await patterns with proper cancellation support

## 📦 Technology Stack

- **Framework**: .NET 10 with ASP.NET Core
- **Database**: Entity Framework Core 10.0.10 with SQLite and PostgreSQL support
- **Authentication**: JWT Bearer
- **Security**: BCrypt for password hashing, TOTP support
- **Observability**: OpenTelemetry for tracing, metrics, and logging
- **API Documentation**: OpenAPI with Scalar UI
- **Testing**: xUnit with NSubstitute for mocking
- **Orchestration**: .NET Aspire for microservices

## 🚀 Getting Started

1. Review [CODING_STANDARDS.md](./CODING_STANDARDS.md) to understand mandatory patterns
2. Explore [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) to learn the monadic patterns
3. Check [DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md) for architectural decisions
4. See [GETTING_STARTED.md](./GETTING_STARTED.md) for setup instructions

## ⚠️ Important Notes

- **No Comments in Code** - If code needs comments, it's bad code. Code should be self-documenting.
- **Extension Methods** - Use C# 12 `extension` syntax with files ending in `Extensions`
- **Immutability** - Favor records, immutable objects, and functional approaches
- **Tests Required** - All new functionality must include unit tests
- **Known Issues** - See [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) for dependency vulnerabilities

## 📖 Documentation Versions

This documentation reflects the current state of the codebase as of the latest commit.

---

**Last Updated**: 2026-09-13  
**Framework Version**: .NET 10  
**Documentation Structure**: Clean, modular markdown files
