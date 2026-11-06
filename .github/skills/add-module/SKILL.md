---
name: add-module
description: 'Add a new module to Behind end to end: its own Common, Domain, WebApi and Database projects (with provider projects, optionally a Tools command line project), loaded by the host project next to the Core module which is required by default, with the module database flattened into the single project database (one database file, one DbContext implementing all module context interfaces, and the project's own database migrator project applying every module migration). USE FOR: new modules, new bounded contexts, new feature areas with their own domain, database and endpoints.'
---

# Add a new module

Load `csharp-code-standards` first. Reference module: `Core` (`src/Core/Behind.Core.*`), loaded by the host project `src/Web/Behind.Web`.

## Rules
- Every module gets its own projects under `src/<Module>/`: `Behind.<Module>.Common`, `Behind.<Module>.Domain`, `Behind.<Module>.Database`, `Behind.<Module>.Database.Sqlite`, `Behind.<Module>.Database.Postgres`, `Behind.<Module>.WebApi`, and optionally `Behind.<Module>.Tools` when the module needs a command line tool. The module has no migrator project of its own: the migrator belongs to the project (see the Migrator checklist step).
- The host project (`src/Web/Behind.Web`) loads every module next to the Core module; Core is required and always loaded by default (`AddCore()`/`UseCore()` in `src/Web/Behind.Web/WebApiExtensions.cs`).
- The database is flattened in the project: one database file and one DbContext implements all the context interfaces of every module (Core and the new module) with the use of both the module implementations, never a separate database or DbContext per module. Because of that the project gets its own database migrator project, which applies the migrations of every module into the one database.
- Modules are separate things: a module never references another module (only the Common projects `Behind.Common*`), and only common things (code every module can use) may be added to the Core module; anything specific to one module stays in that module's projects.
- The database context of a module is an interface (the module context interfaces, composed of the `I<Module>...Commands`/`Queries` interfaces), never an abstract base DbContext class that a module requires. The one project DbContext implements the interfaces of every module, so a site can use more than one module at a time without abstract class collisions (a class can implement many interfaces but inherit only one base class).
- Module-independent code stays in `src/Common/Behind.Common`, `Behind.Common.Database` and `Behind.Common.WebApi`; only module-specific code goes into the module projects.

## Checklist
1. **Domain** (`src/<Module>/Behind.<Module>.Domain`): immutable records, `VersionedKey<Guid>` ids, no own created/updated columns (see `add-entity`).
2. **Common** (`src/<Module>/Behind.<Module>.Common`): module-specific configuration and helpers, like `Behind.Core.Common`. Generic code goes to `Behind.Common` instead.
3. **Database** (`src/<Module>/Behind.<Module>.Database`): the module context interfaces (`I<Module>...Commands`/`I<Module>...Queries`), `Aggregates/`, `Commands/`, `Queries/`, save results and query extensions, following `Behind.Core.Database`, shaped so the single flattened DbContext can implement them. Expose the module context only as interfaces, no abstract DbContext base class. Reuse `Behind.Common.Database` (soft delete, hard delete, restore, auditing, `IUnitOfWork`).
4. **Providers** (`src/<Module>/Behind.<Module>.Database.Sqlite`, `Behind.<Module>.Database.Postgres`): register the single flattened DbContext for the provider with `RegisterSqlite`/`RegisterPostgres` overloads for `WebApplicationBuilder` and `HostApplicationBuilder`, like `CoreSqliteExtensions`. The flattened context implements every module context interface (Core and the new module), applies the aggregate configurations of every module assembly and uses the shared `database.json` connection, so everything lands in the one project database file.
5. **Migrator** (the project's own database migrator project): because the database is flattened, the migrator belongs to the project, not to the module. It composes the module migrations and applies them into the one database through the flattened DbContext, like `Behind.Core.Database.Migrator` does for Core. Create migrations with the `add-ef-migration` skill.
6. **Web API** (`src/<Module>/Behind.<Module>.WebApi`): `WebApiExtensions` with `Add<Module>()`/`Use<Module>()` following `Behind.Core.WebApi/WebApiExtensions.cs`, endpoints under `Endpoints/`. Generic helpers go to `Behind.Common.WebApi`.
7. **Tools (optional)** (`src/<Module>/Behind.<Module>.Tools`): only when the module needs a command line tool. Executable with `System.CommandLine`, `PackAsTool` and a `behind-<module>` `ToolCommandName`, following `Behind.Core.Tools`.
8. **Host**: add the module project reference to `src/Web/Behind.Web/Behind.Web.csproj` and call `Add<Module>()`/`Use<Module>()` in `src/Web/Behind.Web/WebApiExtensions.cs` after Core. The host wires up the one flattened DbContext that implements all module context interfaces with the use of both the module implementations. Add all module projects to `Behind.slnx`.
9. **Entities**: every entity of the module follows the `add-entity` skill (audit labels, soft delete/hard delete/restore, raw database editor via `add-raw-database-editor`, management page via `add-management-page`, uploads via `add-vfs-upload`).
10. **Tests**: module tests in `tests/Behind.Tests` mirroring the module projects, reusing `Helpers/` test beds. Run `dotnet test tests/Behind.Tests/Behind.Tests.csproj` from `Behind/`.
11. **Changelog**: use the `update-changelog` skill.
12. **Docs**: update `Behind/docs/developer/PROJECT_OVERVIEW.md`, `DATABASE_ARCHITECTURE.md`, `DOMAIN_MODEL.md` and `WEBAPI_CONVENTIONS.md`.
