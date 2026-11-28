# Markdown documents list response 1.1.0

`GET /markdownviewer/documents?locale=<locale>` is public and returns a JSON object with HTTP 200. Its [JSON Schema](../../schemas/markdown-documents-response-1.1.0.schema.json) describes the response.

`configured` indicates whether the configured source directory exists. `title` and `defaultDocument` describe the workspace. `locales` contains available language codes; `locale` is the selected language or null. `home` is the home document path or null. `entries` contains document paths and titles; `sections` groups entries by top-level section and has an optional section document path. The requested locale is preferred, followed by the site default and then the first available locale.

`errors` is empty when configured; otherwise it contains `source-not-configured` for an empty or unset source path, or `source-missing` when the resolved directory does not exist. Paths are not disclosed in errors. An unavailable workspace returns empty locales, entries and sections, null locale and home, and the default title and document name.

## Compatibility and migration

Version 1.1.0 adds `errors` to the previously documented response without removing or changing existing fields. Clients that ignore additional properties need no migration. Strict clients must accept `errors`; clients supporting older servers should handle its absence, which does not distinguish an unset path from a missing directory. No persisted data requires migration.
