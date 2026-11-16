---
name: add-ef-migration
description: 'Create an EF Core migration for the Behind core or versioning database (SQLite). USE FOR: schema changes, new tables/entities, data migrations such as MakeNewsVersioned or PublishExistingNews.'
---

# EF Core migration

Load `csharp-code-standards` first.

## Where
- Core context migrations: `Behind/src/Core/Behind.Core.Database.Sqlite/Migrations` (`CoreContextSqlite`, snapshot `CoreContextSqliteModelSnapshot.cs`).
- Versioning context (`ActiveVersions`, `AbstractVersioningContext`): `.../Migrations/Versioning`, its own history table `__VersioningMigrationsHistory`. Do not let it create or change `AuditLogEntries` (mapped with `ExcludeFromMigrations`).
- The migrator (`Behind.Core.Database.Migrator`) applies core migrations first, then versioning ones.

## Steps
1. Change the aggregate configuration in `Behind.Core.Database/Aggregates`.
2. From `Behind/`: `dotnet ef migrations add "<Name>" --project src/Core/Behind.Core.Database.Sqlite --startup-project src/Core/Behind.Core.Database.Migrator` and add `--context` (`CoreContextSqlite` or `VersioningContextSqlite`) plus `--output-dir Migrations/Versioning` for the versioning context. No design-time factory exists in the repo, so if the command fails, check the options with `dotnet ef migrations list` (`GETTING_STARTED.md` names the Database project, but the migrations live in the Sqlite project).
3. Review the generated migration and snapshot. Data migrations must keep existing rows (e.g. existing rows become version 1, audit keys rewritten to `<id>:1` for versioned entities).
4. Migrations are excluded from coverage; still test data migrations that contain custom logic (see `VfsLegacyMigrationTests`).
5. Run `dotnet test tests/Behind.Tests/Behind.Tests.csproj`.
6. Mention schema changes in `docs/en-US/developer/DATABASE_ARCHITECTURE.md` and the changelog (`update-changelog`).

## Module migrations
- A module owns its migration chain: the design-time only `<Module>ContextSqlite`/`<Module>ContextPostgres` of `Behind.<Module>.Database.Sqlite`/`.Postgres` (model = only the module entities, history table `descriptor.MigrationsHistoryTable`, no foreign keys to Core tables). Generate it with `dotnet ef migrations add "<Name>" --project src/<Module>/Behind.<Module>.Database.Sqlite --context <Module>ContextSqlite`.
- The project migrator applies the Core chain, then every module chain (`RegisterSqliteModule`/`RegisterPostgresModule`), then the versioning chain, then the seeds. See `add-module`.
