# Project Overview

## Contact module

The main `Behind.Web` installation loads Contact alongside its existing modules. Contact has its own Common, Domain, Database, SQLite, PostgreSQL and WebApi projects and contributes routes, administrator tools and the `contact.form` layout widget through its descriptor. Existing site installations retain their module selection; installing Contact there also requires flattening `IContactContext` into their database and registering its migrations.

Flattened EF contexts cache models by context type, installed `ModuleCatalog` and design-time state through Common.Database `IModuleModelContext` and `ModuleModelCacheKeyFactory`, configured by `AbstractCoreContext`. Different module selections never reuse an incompatible cached model.

Visitors use `#/contact` or a layout widget to choose an active form and submit a name, email address, subject, message and administrator-defined extra fields. Administrators configure forms and manage a shared request inbox at `#/administration/contact`. Requests persist before email delivery; SMTP failures remain visible for authorized retry. Read state records the administrator who marked the request read.

Common.WebApi's `IModuleEmailSender` is implemented by Core `ModuleEmailSender`, using the reloadable configured email provider without a module-to-Core dependency. The Logger provider reports success after logging, without sending to a mailbox. All submitted data is HTML-encoded in email and rendered as text in the inbox.

## Optional content locales

Content locales are part of the existing editors, not a separate administration application. Routes do not contain a locale; the selected locale is sent with content requests. The default comes from the signed-in user's profile, browser cache or site default. Content without a locale is shared with all selected locales; locale-assigned content is filtered for public feeds, widgets and installed modules. Locale choices use the available interface languages. Sites and route aliases have locale-scoped slug uniqueness, with shared slugs reserved across locales. Layouts expose `core.locale-switch` and an optional header switch. For signed-in users, changing the switch also saves the selected interface language to the profile.

## Layouts and widgets

Scheduled site notices are managed at `#/manage/messages` under the `Message` grant area. They are versioned database entities with levels and optional visibility dates; `core.message` widgets are placed manually in layouts and each selects a message. Several notices may be displayed simultaneously. Dismissal is browser-local and version-specific; the built-in footer remains unchanged.

Core provides `#/manage/layouts` (Administration > Layouts), a grid editor with multiple layouts, section ordering, row/column spans, placement preview and exact/prefix route assignments. The engine renders complete public pages with optional `core.header` and `core.footer` widgets; no fixed public chrome surrounds the layout. New configurations include header, `core.content` and footer sections. The shell preserves existing page markup and bindings by moving the main container into the content slot. Account, administration and restricted-access pages use a fixed shell using the same chrome components, except that saving previews the selected layout around the editor until navigation. The shell wraps every section in a `.behind-layout-band`: site sections sit in a Bootstrap `.container` (`behind-layout-section-site`), full sections span the viewport (`behind-layout-section-full`). Section and widget `cssClass` values customize their rendered wrappers without affecting editor controls. Both installation skins inherit the shared responsive layout styles.

Style header/footer widgets through `.behind-page-header` and `.behind-page-footer`, not only the fixed-shell `#header-container` and `#footer-container` ids. Repeated header widgets receive unique navigation-collapse ids.

Layouts also contain optional text or linked-image badges outside the grid. Their absolute `.behind-layout-badges` overlay sits above sections and menus but scrolls with the page; only individual badges accept pointer events. Corner groups stack badges in list order and constrain their width on mobile. Images reuse layout image upload, validation and VFS attachment; account and management shells never mount the badge overlay.

Modules contribute widgets through `ModuleDescriptor.Widgets` (`ModuleWidget` in Common), exposed by `GET /modules`. Keys are module-prefixed and frontend loaders belong to that module. Core supplies latest content, menus, custom rich text/HTML and chat; Forum contributes posts and statistics only when installed. Widget endpoints apply their normal access rules. `models/invalidation` refreshes subscribed regions without a browser reload; menu mutations invalidate mounted menus, and layout saves invalidate the shell without replacing the current content.

Module frontend models export `load(type, limit)` returning `{items?, stats?, content?, messages?}`. Descriptor fields are `key`, locale-key `name` and AMD `model`. Chronological module feeds inject `IModuleCreationOrder.NewestFirst` from Common.Database; Core implements it using audit creation times, so modules never sort UUIDs or depend on Core. `CreatedAtAsync` batch-reads first-version creation times, returning null when an audit entry is missing.

Interactive module widgets may accept a third `widget` placement argument and return `{component: {name, params}}` after registering a module-owned Knockout component. The shell mounts through the component binding, preserving component disposal and clearing it on identity changes or loading errors. Existing list/statistics models remain compatible; no module-specific Core dependency is needed.

## Forum and public profiles

Global age enforcement is controlled by reloadable `Backend.ContentRestrictions.Enabled` (false by default). Versioned per-item rules use the shared filtered context, including transitive parent references, media, feeds and counts. Module descriptors declare `ModuleDatabase.ContentRestrictedEntities` and `ContentRestrictionParentProperties`, avoiding dependencies on Core. Module editors use the shared age control and the locale edit/ownership permissions together with `ContentRestriction.Write`; selectable ages are administered separately with `ContentAge.Manage`.

The main `Behind.Web` installation loads `Behind.Forum.*` beside Core, Quotes and Sport; the existing sites do not load Forum. Its Common, Domain, Database, SQLite, PostgreSQL and WebApi projects contribute through `ForumModule.Descriptor`. The frontend includes board/topic/reply browsing and forum administration with moderation. `forum.json` is reloadable and appears on Settings.

Core exposes session identity through `IModuleIdentity` and public display labels/picture ids through `IModuleUsers`; labels use a trimmed name plus a stable UUID-derived tag, or `user#tag` when blank, never an email address. Modules never reference Core. Core's Profile page uploads/removes pictures using the existing VFS and Bootstrap styling, shared by every installation. Public author links target `#/user/<id>`, with permission-checked administrator moderation and impersonation. Birthday is optional/private and email sharing is opt-in.

Run feature tests with `dotnet test tests/Behind.Tests/Behind.Tests.csproj --filter "FullyQualifiedName~Forum|FullyQualifiedName~ProfilePicture"` and the frontend runtime tests with `node --test tests/frontend-runtime-tests/*.test.cjs` from `Behind/`. The Node tests use the bundled Knockout and production pager, without additional packages.

Behind is a modern .NET 11 application built with clean architecture principles, functional programming paradigms, and emphasis on code quality and maintainability.

## Project Purpose

Behind is designed as a scalable, maintainable backend system using microservices architecture (via .NET Aspire) with:
- Strong domain-driven design
- Functional programming patterns
- Type-safe error handling
- Comprehensive API documentation
- Production-ready observability

## Technology Stack

### Core Framework
- **Runtime**: .NET 11
- **Web Framework**: ASP.NET Core 11.0.0
- **Language Features**: C# 15 (records, extension members, top-level statements)

### Data Access
- **ORM**: Entity Framework Core 11.0.0-rc.3.26522.148
- **Database Support**:
  - SQLite 3.53.3 (`SQLitePCLRaw.lib.e_sqlite3` 3.53.3)
  - PostgreSQL (Npgsql.EntityFrameworkCore.PostgreSQL 11.0.0-rc.3)
- **Migration**: EF Core Code Migrations via Behind.Core.Database.Migrator

### Security & Authentication
- **Authentication**: JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer)
- **Password Hashing**: BCrypt.Net-Next 4.2.0
- **Two-Factor Authentication**: Otp.NET 1.4.1 (TOTP support)

### Observability & Monitoring
- **Tracing & Metrics**: OpenTelemetry 1.17.0
- **Instrumentation**: 
  - ASP.NET Core
  - HTTP Client
  - Runtime
- **Export**: OTLP (OpenTelemetry Protocol) compatible exporters

### API Documentation
- **OpenAPI**: Microsoft.AspNetCore.OpenApi 11.0.0
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
- **Service Hosting**: Aspire 13.6.5
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

## Modules

Behind is a Core module plus any number of optional modules. The host project (`src/Web/Behind.Web`) loads the modules next to Core: `AddCore()` is required and a module adds itself with `services.AddModule(descriptor)`, whatever the order, and `UseCore()` serves the frontend files of every module.

A module describes everything the application needs to integrate it in one immutable `ModuleDescriptor` (`Behind.Common/Modules`):

| Contribution | Descriptor member | Effect in Core |
|---|---|---|
| Grants | `GrantAreas` (`ModuleGrantArea(area, audience, actions)`) | grants, restrictions and authorization policies are created for every action, the built-in roles receive them by audience (Everyone: Administrators all actions, Users `Read`; Administration: Administrators only; Developer: Developers `Read`/`Manage`), and they are listed on the Roles and Users pages |
| Default menu items | `MenuItems` | ensured in the main menu by the migrator (`Link` or `User` kind, visible to everybody, to the administration roles or to developers) |
| Administration items | `AdministrationPages` (name key, icon, `#/` href, grant) | shown on the Administration page for the users who hold the grant |
| Pages | `Routes` (`#/` path, controller, `Open`) | registered by the frontend router before the route alias fallback |
| Frontend files and styles | `Frontend` | embedded `Frontend/` files merged into the Core static files, namespaced under `modules/<key>/` |
| Database | `Database` | entity configurations, soft delete and read-only raw tables of the flattened DbContext |

`GET /modules` (open, like `/about`) publishes the routes, administration pages and styles to the frontend, and `ModuleCatalog.Create` validates the contributions at startup (key and area patterns, duplicate grant areas, routes and menu item ids, controllers and styles under `modules/<key>/`); `AccessCatalog.From` additionally rejects areas and menu item ids already used by Core and administration pages that need an unknown grant.

See `.github/skills/add-module/SKILL.md` for the full checklist and `DATABASE_ARCHITECTURE.md` for the flattened database and the migrations.

### Example module: Quotes

`src/Quotes` is a complete, deliberately small example module (a list of quotes with an author) that shows every part of the module checklist. It is loaded by the host `src/Web/Behind.Web` next to Core:

| Project | Holds |
|---|---|
| `Behind.Quotes.Domain` | the immutable `Quote` record (`VersionedKey<Guid>` id, text, author) |
| `Behind.Quotes.Common` | `QuotesModuleNames`: the module key, grant area, grant names, routes and the menu item id |
| `Behind.Quotes.Database` | `IQuotesContext`, `QuoteConfiguration`, `IQuoteCommands`/`IQuoteQueries` and their implementations, `QuotesAuditLabelSource`, `QuotesSeed`, `AddQuotesDatabase()` |
| `Behind.Quotes.Database.Sqlite`, `Behind.Quotes.Database.Postgres` | the design-time only migration contexts, their design-time factories and the `CreateQuotes` migrations |
| `Behind.Quotes.WebApi` | `QuotesModule.Descriptor`, the `/quotes` endpoints, `AddQuotes()`/`UseQuotes()` and the embedded frontend under `Frontend/modules/quotes/` |

The project side lives in `src/Web`: `Behind.Web.Database.Sqlite`/`.Postgres` hold the one DbContext per provider (`WebContextSqlite`, `WebContextPostgres`: `AbstractCoreContext` plus `IQuotesContext`), `Behind.Web.Database.Migrator` is the migrator of the project (Core migrations, then the Quotes migrations, then the seeds), and `Behind.Web` calls `AddQuotes()`/`UseQuotes()` after `AddCore()`/`UseCore()`.

### Module: Sport

`src/Sport` is a module for sports (soccer first) with sports, positions, stat types, seasons, teams, players, season memberships and statistics. It follows the Quotes structure (`Behind.Sport.Domain`, `.Common`, `.Database`, `.Database.Sqlite`, `.Database.Postgres`, `.WebApi`) and shares its generic parts with every module through the Common projects: `VersionedCommands<TEntity, TDraft>`/`IVersionedCommands<TDraft>`/`VersionedSaveResult` (versioned create, update, soft delete, restore, hard delete, ensure), `Keyed<TEntity>`, `IModuleFiles` (VFS uploads for modules), `ImageUploadExtensions` (image upload and serving endpoints helpers) and `VersionedSaveResultExtensions`. The project contexts implement `ISportContext` and `Behind.Web.Database.Migrator` applies the Sport migrations after the Quotes ones. The frontend lives under `Frontend/modules/sport/` with the public pages (`#/sport`, teams, players, seasons, leaderboards, stats, every entity linked everywhere) and one generic management page (`#/manage/sport/<resource>`).

The FSE site (`src/Sites/Behind.Sites.FSE`) loads the Sport module: `Behind.Sites.FSE.Database.Sqlite`/`.Postgres` hold its one DbContext per provider (`FseContextSqlite`, `FseContextPostgres`: `AbstractCoreContext` plus `ISportContext`), `Behind.Sites.FSE.Database.Migrator` is its migrator (Core migrations, then the Sport migrations, then the seeds) and `AddFse()`/`UseFse()` call `AddSport()`/`UseSport()` after `AddCore()`/`UseCore()`.

### Module without a database: Markdown Viewer

`src/MarkdownViewer` is a module that needs no database table, so it has no database projects and no `ModuleDescriptor.Database`: it works on a source directory and an output directory of the server instead of the VFS. It is loaded by `src/Web/Behind.Web` with `AddMarkdownViewer()`/`UseMarkdownViewer()`:

| Project | Holds |
|---|---|
| `Behind.MarkdownViewer.Domain` | the immutable `MarkdownEntry`, `MarkdownPage` and `GeneratedSite` records |
| `Behind.MarkdownViewer.Common` | `MarkdownViewerModuleNames`, `MarkdownViewerConfiguration` (the `MarkdownViewer` section of `markdownviewer.json`) and `MarkdownWorkspace`/`MarkdownGeneration`, the validated directories (`ForViewing`/`ForGeneration`, `MarkdownWorkspaceErrors`) |
| `Behind.MarkdownViewer.WebApi` | `MarkdownViewerModule.Descriptor` (declares `ConfigurationFiles = ["markdownviewer.json"]`), the Markdig rendering with link rewriting and `SanitizeMarkdown()`, the `StaticSite` generator, the `/markdownviewer` endpoints and the embedded frontend under `Frontend/modules/markdownviewer/` |

The Settings page shows the `markdownviewer.json` tab because the module declares the file in its descriptor (`ConfigurationFileCatalog.For(root, moduleNames)`). Only the files of the source directory that `MarkdownSourceExtensions` enumerates (no hidden files, no symbolic links, `.md` and a fixed list of image types) can be read, so a path can never leave the source directory. Language folders (`^[a-z]{2,3}-[A-Z]{2}$`) directly inside the source directory make it localized (`MarkdownLocaleExtensions`: `Locales`, `ForLocale`, `Candidates(requested, default)`: requested, then `Frontend:Site:DefaultLocale`, then the first language); the frontend sends `i18n.locale()` and reloads when the language selector changes, and the generator writes one site per language. The generator writes a `.markdownviewer-generated` manifest into the output directory and removes only the files listed in it.


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

### 3. Extension Members (C# 14+ `extension(Type)` syntax)
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
- Dev container with .NET 11 SDK pre-configured
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

### Versions in Use
- .NET SDK: 11.0.100
- ASP.NET Core and Microsoft.Extensions: 11.0.0
- Entity Framework Core: 11.0.0-rc.3.26522.148
- OpenTelemetry: 1.17.0
- Microsoft.AspNetCore.Authentication.JwtBearer: 11.0.0

### Known Limitations
- See [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) for dependency vulnerabilities
- `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages (verified 2026-11-12)

---

**Next Steps**:
- Review [CODING_STANDARDS.md](./CODING_STANDARDS.md) for mandatory practices
- Study [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) for pattern understanding
- Check [DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md) for architectural decisions
