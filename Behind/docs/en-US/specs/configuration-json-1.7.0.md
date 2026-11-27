# Configuration JSON and settings 1.7.0

Status: current. This additive revision supersedes
[configuration 1.6.0](./configuration-json-1.6.0.md) for runtime validation.
The complete [JSON Schema](../../schemas/configuration-1.7.0.schema.json)
retains the previous definitions and introduces module switches and documented
module defaults. Previous schemas and specifications remain unchanged.

## Covered configuration

The schema covers Backend (accounts, modules, comments, downloads, scheduler,
forwarded headers, content restrictions and audit budgets), Frontend (site,
sites, galleries, carousels and static files), Security (accounts, notifications,
JWT, magic links, elevation, data protection and OIDC), Database, Caching,
ConnectionStrings, Email, Serilog minimum levels, OpenTelemetry, Forum and
MarkdownViewer. Converted values retain their existing JSON shapes and existing
domain validation remains authoritative. Additional custom properties remain
supported.

## Module switches

`backend.json` accepts an optional `Backend.Modules` object whose lowercase
descriptor keys map to JSON booleans:

```json
{
  "Backend": {
    "Modules": {
      "contact": true,
      "quotes": true,
      "sport": true,
      "forum": false,
      "markdownviewer": true
    }
  }
}
```

Missing switches, a missing section and a null section mean enabled. Core is
required and ignores attempts to disable it. Settings only offers installed
optional module keys and rejects unknown or Core switch updates.

Switches reload without a server restart. Every module API is identified by
`ModuleEndpoint` metadata; disabled requests and module frontend assets return
404 before invoking their handlers, with no-store caching. Reloading a switch
to false cancels active module request tokens. Completed operations are not
undone; cancellation remains cooperative.

`GET /modules` keeps its existing response shape but omits disabled descriptors.
Their routes, administration pages, styles and widgets are not discovered.
Public menu links, including localized and nested links, and layout widgets
are filtered. Shared report evidence, content-locale/age editing and raw table
entrypoints also enforce availability.

The installed catalog and flattened EF model do not change. Tables, files,
versions, audit history, grants and configuration remain intact. Module settings
remain accessible to authorized developers so modules can be re-enabled.
Saving switches refreshes the current browser; other open browsers must reload
to remove already rendered content. Server access is blocked on configuration
reload regardless of browser state.

## Complete settings discovery and updates

`GET /settings` requires Configuration.Read and returns the
[settings discovery 1.0.0 schema](../../schemas/settings-response-1.0.0.schema.json):

```json
{
  "readOnly": false,
  "files": [{
    "name": "backend.json",
    "editor": null,
    "fields": [{
      "path": "Backend:Scheduler:Enabled",
      "type": "boolean",
      "value": "true",
      "sensitive": false,
      "editable": true,
      "action": null,
      "options": null
    }]
  }]
}
```

Every known property of an existing configuration file is included even when
absent or null. Displaying defaults never writes them to disk. Unknown populated
scalar fields remain available. Dedicated email, logging/OpenTelemetry, OIDC and
forwarded-header editors retain their validation and secret handling.

Field types are `string`, `number`, `boolean`, `array` or `object`. Values remain
strings in the discovery response; arrays and maps contain JSON text and have
validated JSON editors. Optional choices use `{ value, label }` options.
`action` is null, `generate` or `rotate`; action-managed secrets cannot be typed.
Existing property casing is preserved without introducing duplicate paths.

`PUT /settings/{file}` requires Configuration.Manage and accepts
`{ "values": { "<colon-separated path>": <typed JSON value> } }`.
Only changed values are written. Known missing fields can be created; unknown
missing fields, wrong value types, overlapping paths and case-equivalent
duplicate paths are rejected with 400. Full candidate documents are validated
before replacing the file. Unknown existing array entries remain editable.
The appsettings editor submits its existing logging/OpenTelemetry payloads
together with additional scalar changes, without allowing those scalar changes
to overwrite the dedicated editor sections.

Secrets are masked. Connection maps preserve unchanged masked values and blank
secret replacements; removing an entry or explicitly setting it to null clears
it. Empty new connections retain the existing fallback semantics. Duplicate
case-equivalent connection names are rejected. Generating a missing JWT signing
key creates it; rotating an absent magic-link secret initializes it while
preserving configured limits. Automatic rotation still skips unconfigured links.

Read-only configuration rejects all writes and secret actions with 403. File
and permission errors retain their existing status codes. Invalid JSON editors
show a localized error and do not send an update.

## Defaults, compatibility and migration

Core defaults come from configuration records; module-specific defaults are
declared in the schema (Forum moderation `Post`, Documentation title
`Documentation`, default document `README.md`). Startup generation includes
module switches and the default compressed MIME-type list.
`Frontend.StaticFiles.CompressedMimeTypes` now replaces, rather than appends to,
the default list; an explicit empty array disables compression for all types.
Previously omitted lists retain the defaults. Installations that relied on
implicit appending must list every desired MIME type.

No database migration is necessary. Existing files can omit all new keys.
Deploy the backend and frontend together: older clients do not have array/map
editors. Startup-only database, caching, certificate, OIDC and telemetry settings
still require their documented restarts; module switches do not.

Regression tests cover missing defaults, type checks, collection updates, secret
preservation, initialization, all actual module endpoints and assets, cancellation,
public navigation/layout filtering and installed database-model retention.
