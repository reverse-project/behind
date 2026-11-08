---
name: add-vfs-upload
description: 'Add or change anything that uploads or serves files in Behind using the VFS (virtual file system): database metadata, content under the data directory from backend.json, parent assignment, per-content-type URL. USE FOR: image/file uploads for sites, news, articles, categories, galleries, downloads or any new content kind.'
---

# VFS file uploads

Load `csharp-code-standards` first. Code: `AbstractCoreContextVfs`, `IVfsCommands`, `VfsFile` (`Behind.Core.Domain`), `VfsStoreTests`, `SiteImageAttachTests`.

## Rules
1. All upload-related things use their own VFS in the database: every file has an id and all known metadata. Do not create ad hoc image/file tables.
2. File content lives in the data directory configured in `backend.json` (`Behind/src/Web/Behind.Web/backend.json`, `examples/container-compose/backend.json`), never in the database.
3. Content path: `vfs/<Type>/<parent item>/<item>`, where `<Type>` is the kind (`Sites`, `News`, `Articles`, `NewsCategories`, `ArticleCategories`, `Galleries`, `Downloads`, ...) and `<item>` is the file uuid. Example: `vfs/Articles/<article uuid>/<file uuid>`. While there is no parent yet: `vfs/<Type>/<item>`.
4. **Set the parent as soon as it is known**, in every place that uses a file: insert a new version of the file and move the content under the parent. Every new place using uploads must do this.
5. **Own URL per content kind**: `/sites/images/<id>`, `/news/images/<id>`, `/articles/images/<id>`, `/galleries/images/<id>`. The upload endpoint answers with that URL and content refers to it. Never serve through another kind's URL; a new kind gets its own route.
6. Modules use `IModuleFiles` (`Behind.Common.Database/Modules`) instead of Core `IVfsCommands`, with their own VFS type (for example `SportTeams`, `SportPlayers`) and own URLs (`/sport/teams/images/<id>`); `ImageUploadExtensions` in `Behind.Common.WebApi` provides the upload and serve helpers.
7. Helpers shared by several kinds go to `Behind.Common.WebApi` (e.g. `FormFileExtensions`, `AttachmentResponseExtensions`).

## Tests
Cover upload, parent assignment (file moved to the parent path), serving via the kind's own URL, and the legacy/migration paths when touched. Use `Helpers/VfsTestPaths.cs` and `TestImages.cs`.

## Finish
`update-changelog`; docs in `DATABASE_ARCHITECTURE.md` / `WEBAPI_CONVENTIONS.md`.
