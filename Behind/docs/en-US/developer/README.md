# Behind Codebase Documentation

Comprehensive documentation for the Behind project - a modern, clean architecture .NET application following functional programming paradigms, SOLID principles, and Clean Code practices.

## 📚 Documentation Index

### Core Documentation
- **[Project Overview](./PROJECT_OVERVIEW.md)** - Project structure, technology stack, and overall architecture
- **[Design Patterns](./DESIGN_PATTERNS.md)** - Architectural patterns and design decisions
- **[Coding Standards](./CODING_STANDARDS.md)** - Mandatory coding standards, conventions, and best practices
- **[Functional Programming](./FUNCTIONAL_PROGRAMMING.md)** - Functional programming patterns and monads used throughout the codebase

### Architecture Guides
- **[Database Architecture](./DATABASE_ARCHITECTURE.md)** - Database layer, Entity Framework patterns, and repository design
- **[Web API Conventions](./WEBAPI_CONVENTIONS.md)** - Web API configuration, middleware, and OpenAPI documentation
- **[Domain Model](./DOMAIN_MODEL.md)** - Domain models, aggregates, and entity design
- **[Email Sender Implementation](./EMAIL_SENDER_IMPLEMENTATION.md)** - Email sender configuration, implementation details, and usage guide

### Operational Guides
- **[Limitations and Issues](./LIMITATIONS_AND_ISSUES.md)** - Known limitations, current issues, and dependency vulnerabilities
- **[Getting Started](./GETTING_STARTED.md)** - Setup, build, run, and development workflow
- **[Behind Core Tools](./tools/README.md)** - Commands for startup configuration, TOTP secrets, and release notes
- **[Container Publishing Scripts](./tools/container-publishing.md)** - Build and publish the application's container images

## 🎯 Quick Navigation

**New to the project?** Start with [Project Overview](./PROJECT_OVERVIEW.md)

**Want to understand the code patterns?** Read [Coding Standards](./CODING_STANDARDS.md) and [Functional Programming](./FUNCTIONAL_PROGRAMMING.md)

**Need to work with data?** See [Database Architecture](./DATABASE_ARCHITECTURE.md)

**Building APIs?** Check [Web API Conventions](./WEBAPI_CONVENTIONS.md)

**Aware of current issues?** Review [Limitations and Issues](./LIMITATIONS_AND_ISSUES.md)

**Need command-line setup or release notes?** See [Behind Core Tools](./tools/README.md)

**Publishing container images?** See [Container Publishing Scripts](./tools/container-publishing.md)

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
    ├── en-US/               # English documentation
    │   ├── developer/       # This documentation
    │   └── user-manual/     # End user manuals
    └── hu-HU/               # Hungarian documentation (same structure)

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

1. Review [Coding Standards](./CODING_STANDARDS.md) to understand mandatory patterns
2. Explore [Functional Programming](./FUNCTIONAL_PROGRAMMING.md) to learn the monadic patterns
3. Check [Design Patterns](./DESIGN_PATTERNS.md) for architectural decisions
4. See [Getting Started](./GETTING_STARTED.md) for setup instructions
5. Use [Behind Core Tools](./tools/README.md) to generate startup configuration, TOTP secrets, or release notes
6. Use [Container Publishing Scripts](./tools/container-publishing.md) to publish container images

## ⚠️ Important Notes

- **No Comments in Code** - If code needs comments, it's bad code. Code should be self-documenting.
- **Extension Methods** - Use C# 12 `extension` syntax with files ending in `Extensions`
- **Immutability** - Favor records, immutable objects, and functional approaches
- **Tests Required** - All new functionality must include unit tests
- **Known Issues** - See [Limitations and Issues](./LIMITATIONS_AND_ISSUES.md) for dependency vulnerabilities

## 📖 Documentation Versions

This documentation reflects the current state of the codebase as of the latest commit.

---

**Last Updated**: 2026-09-13  
**Framework Version**: .NET 10  
**Documentation Structure**: Clean, modular markdown files
