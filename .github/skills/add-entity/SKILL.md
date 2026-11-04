---
name: add-entity
description: 'Add a new persisted entity (or start using an entity somewhere new) in Behind end to end: VersionedKey<Guid> id, audit-based created/updated times, audit label resolution, soft delete/hard delete/restore, commands, queries, migration, endpoints, tests, changelog. USE FOR: new domain entities, new aggregates, new management resources like Sites, News, Articles, Galleries, Downloads, Menus.'
---

# Add a new entity

Load `csharp-code-standards` first. Reference implementations: `Site`, `News`, `Download` (commit `e0f7485` shows a full feature, 73 files) and `Article`.

## Checklist
1. **Domain** (`src/Core/Behind.Core.Domain`): immutable record. `Id` is `VersionedKey<Guid>` (Key + Version, composite key). A save inserts the next version and never updates a row (`ISiteCommands.Create`/`SaveVersion` pattern, version conflict result like `SiteSaveResult.VersionConflict`). Add `GrantName`/`BuiltInAccess`/`BuiltInMenus` entries when the page needs access control or a menu item.
2. **No own timestamps**: never add created/updated columns. Created and latest Updated come from the audit log (`AuditTimeExtensions`, `AuditTimes`). Responses read them from there.
3. **Database** (`src/Core/Behind.Core.Database`): aggregate configuration in `Aggregates/`, `AbstractCoreContext<Entity>.cs` partial, `I<Entity>Commands`, `I<Entity>Queries`, `Commands/`, `Queries/`, `<Entity>QueryExtensions`, `<Entity>SaveResult`. Register in `AbstractCoreContext`.
4. **Soft delete, hard delete, restore** (see `AbstractCoreContextRestore`, `AbstractCoreContextHardDelete`): delete soft-deletes, developers can list deleted records, restore them and delete them permanently.
5. **Audit labels** (mandatory): update `Behind.Core.Database/AuditLabelExtensions.cs` so the audit log UI shows a readable name instead of a UUID, and add tests in `tests/Behind.Tests/CoreDatabase/Queries/AuditLogEntryLabelsTests.cs`. Do this also when an existing entity is used somewhere new.
6. **Migration**: use the `add-ef-migration` skill.
7. **Web API** (`src/Core/Behind.Core.WebApi/Endpoints`): `<Entity>Endpoints`, `<Entity>Response` (+ `ResponseExtensions`), `<Entity>RoutesExtensions` (route mapping), register in `WebApiExtensions.cs`. Generic helpers go to `Behind.Common.WebApi`.
8. **Uploads**: if the entity holds files, use `add-vfs-upload`.
9. **Frontend page**: use `add-management-page`.
10. **Raw database editor**: every entity must be editable in the raw database editor, use `add-raw-database-editor` (`[Sensible]` columns, read-only tables, column kinds, `RawDatabase*` tests).
11. **Tests**: domain, commands, queries, endpoints, routes, frontend tests, audit labels; reuse/add a `Helpers/<Entity>TestBedExtensions.cs`.
12. **Changelog**: use `update-changelog`.
13. **Docs**: update `Behind/docs/developer/DATABASE_ARCHITECTURE.md`, `DOMAIN_MODEL.md`, `PROJECT_OVERVIEW.md`, `WEBAPI_CONVENTIONS.md` and a page in `docs/user-manual/` (plus its README/administration index) when relevant.
14. Run `dotnet test tests/Behind.Tests/Behind.Tests.csproj` from `Behind/`.
