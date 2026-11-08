---
name: add-raw-database-editor
description: 'Keep the RAW database entry editor of the Behind management UI (Administration > Raw database, #/manage/raw-database, RawDatabase.Manage) working for every entity: new entities, new tables, new columns, sensitive columns, read-only tables, custom value converters. USE FOR: every new or changed persisted entity, join table or column, and any change to the raw editor itself.'
---

# Raw database entry editors

Load `csharp-code-standards` first. Every persisted entity must be editable in the raw database editor: developers (`RawDatabase.Manage`) list, search, sort, insert, edit and permanently delete the stored rows of every table. The editor is generic and driven by the EF Core relational model, so most entities need no editor code, only the checks below.

## How it works
- `Behind.Common.Database/Raw`: `DbContext.RawTables` turns every table with a primary key into a `RawTable` / `RawColumn` (kind by provider type, key, nullable, `IsSensitive` from `[Sensible]`); `RawDatabaseExtensions` runs parameterised SQL; `RawColumnExtensions` parses, normalises and masks values.
- `Behind.Core.Database`: `IRawDatabaseQueries` / `IRawDatabaseCommands` (`RawDatabaseQueries`, `RawDatabaseCommands`). Writes run in a transaction and add their own audit entry (`Created`, `Updated` differences, `HardDeleted`) because raw SQL bypasses the change tracker. `RawDatabasePolicyExtensions` lists the read-only entities.
- `Behind.Core.WebApi/Endpoints`: `RawDatabaseEndpoints`, `RawDatabaseResponse(Extensions)`, `RawDatabaseRoutesExtensions` (`/raw-database/...`, all `RawDatabase.Manage`).
- Frontend (`src/Core/Behind.Core.WebApi/Frontend`): `models/rawdatabase.js`, `viewmodels/rawdatabase.js`, `views/rawdatabase.html`, `controllers/rawdatabase.js`, route `#/manage/raw-database` in `app.js`, tile in `viewmodels/administration.js`.

## Checklist for a new entity (or join table, or column)
1. **Map it in `AbstractCoreContext`** (`add-entity`). A table with a primary key appears in the editor automatically; nothing to register. A table without a primary key is not editable, so give every new table a key.
2. **Sensitive data**: mark every property that holds a secret (password hash, token, key) with `[Sensible]` so the editor masks it (`***`), never searches it and audits it masked. Do this on the entity property, also for complex properties.
3. **Read-only tables**: if rows must never be edited by hand (like `AuditLogEntries`), add the entity name to `RawDatabasePolicyExtensions.ReadOnlyEntities`. Everything else stays editable.
4. **Column kinds**: `RawTableExtensions.Kinds` maps the provider CLR type (after value converters) to a `RawValueKind`. If the new column uses a provider type that is not listed (for example a new numeric type), add it there, or it is edited as text. Add a `RawColumnExtensionsTests` case when a new kind is parsed.
5. **Audit names**: the editor audits with the same entity name and key as the EF audit (`IReadOnlyEntityType.AuditEntityName`). Update `AuditLabelExtensions` and its tests as `add-entity` requires, so raw edits of the entity show readable labels.
6. **Tests** (`tests/Behind.Tests`): `CoreDatabase/RawDatabaseCatalogTests` already fails when the model has an entity type outside the editor, when a table has no key and when a table cannot be listed, searched and sorted, run it. Add a `RawDatabaseCommandsTests` case when the entity has a rule of its own (sensitive column, composite key, generated key, read-only).
7. **Frontend**: the page is generic, no per entity files. If the entity name needs a readable title in the audit log, `audit.entities.<Name>` in both locales as `add-entity` requires. User visible text of the editor changes go to every locale in `Frontend/locales`.
8. **Docs and changelog**: mention the entity in `docs/en-US/developer/DOMAIN_MODEL.md` / `DATABASE_ARCHITECTURE.md` as `add-entity` requires; `update-changelog` for editor behavior changes; `docs/en-US/user-manual/managing-raw-database.md` when the editor itself changes.
9. Run `dotnet test tests/Behind.Tests/Behind.Tests.csproj --filter "FullyQualifiedName~RawDatabase"` from `Behind/`, then the full suite.

## Rules of the editor (keep them when changing it)
- Table and column names only come from the model, values are always SQL parameters, `LIKE` terms are escaped.
- Key columns cannot be changed by an update; a Guid key left out of an insert is generated.
- Sensitive values are masked in every read, response and audit difference; a masked `***` input never overwrites a stored value.
- Every raw write and its audit entry are saved together or not at all; database errors answer `409` with `rejected`, never a stack trace.
- Soft delete does not apply: raw deletes remove the row for good and are audited as `HardDeleted`.
- No comments in code, extension(...) blocks in `*Extensions.cs` files, braces everywhere (`csharp-code-standards`).

## Modules
Module tables join the flattened project database and show up in the raw editor automatically. Mark sensitive properties with `[Sensible]` and list read-only entity names in `ModuleDatabase.ReadOnlyEntities` of the module descriptor (`AbstractCoreContext.IsReadOnly(table)` merges them with the Core ones).
