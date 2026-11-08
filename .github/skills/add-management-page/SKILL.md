---
name: add-management-page
description: 'Add or change a management page in the Behind embedded frontend (users, roles, sites, news, articles, galleries, downloads, menus, ...) with soft delete, hard delete and restore. USE FOR: new admin/management UI, new frontend views, viewmodels, models, components, routes and frontend tests.'
---

# Add a management page

Load `csharp-code-standards` first. Frontend root: `src/Core/Behind.Core.WebApi/Frontend`. Reference: the Downloads feature (commit `e0f7485`).

## Mandatory behavior
Every management page (users, roles, sites and any new one) must:
- **Soft delete** on delete.
- Let developers **list deleted** records.
- **Restore** deleted records.
- **Hard delete** (delete permanently).

The backend must expose matching endpoints (see `add-entity`).

The entity behind the page must also be editable in the generic raw database editor (`#/manage/raw-database`), see `add-raw-database-editor`; the management page itself never replaces it.

## Files
- `views/<name>.html`, `viewmodels/<name>.js`, `controllers/<name>.js`, optional `models/<name>.js`, `components/<name>.html|js`.
- Register the route/view in `app.js`; add menu entry on the backend in `BuiltInMenus` and the administration listing in `viewmodels/administration.js` when needed.
- Styles in `css/app.css`.
- Created/updated times shown in the UI come from the audit log, not from entity fields.

## Tests
Add `tests/Behind.Tests/CoreWebApi/<Name>FrontendTests.cs` (see `DownloadsFrontendTests.cs`, `NewsFrontendTests.cs`, `RouterFrontendTests.cs`, `EmbeddedFrontendTests.cs`), plus route tests.

## Finish
- Changelog entry via `update-changelog`.
- User manual page in `Behind/docs/en-US/user-manual/managing-<name>.md` and links in its `README.md` / `administration.md`.
- If a frontend library, css or webfont is added or upgraded, use `update-licenses`.

## Pages of modules
A module adds its pages under `Frontend/modules/<key>/` (RequireJS ids `modules/<key>/controllers/...`) and declares them in its `ModuleDescriptor`: `Routes` and `AdministrationPages` (name locale key, icon, `#/` href, required grant). Locale keys go to `Frontend/modules/<key>/locales/<code>.json`. See `add-module`.
