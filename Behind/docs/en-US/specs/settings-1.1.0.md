# Settings discovery and updates 1.1.0

Status: current. This revision supplements the configuration contract in
[configuration 1.7.0](./configuration-json-1.7.0.md) and supersedes its
settings discovery 1.0.0 behavior. The configuration-file schema is unchanged.

## Discovery

`GET /settings` requires `Configuration.Read` and returns the
[settings response schema](../../schemas/settings-response-1.1.0.schema.json).
It lists existing configuration files and fields, including defaults for
missing known settings. Field values are strings; `array` and `object` values
contain JSON text. Displayed defaults are not written until changed.

For `appsettings.json`, `Serilog:Using`, `Serilog:Enrich` and
`Serilog:WriteTo`, `Serilog:AuditTo`, `Serilog:Filter` and
`Serilog:Destructure` are array fields rather than flattened array leaves.
Missing or null arrays show the embedded defaults, or an empty array when
there is no embedded default. A non-array section
remains available as its existing scalar leaves. Unknown properties are retained.
Nested values with sensitive property names are masked in collection responses;
the containing field's `sensitive` flag does not describe every child.
The existing logging and OpenTelemetry editors remain authoritative for their
own sections.

## Updates

`PUT /settings/{file}` requires `Configuration.Manage`; its request follows the
[settings update schema](../../schemas/settings-update-1.1.0.schema.json), with
`OrdinaryFileUpdateRequest` for ordinary files and `AppSettingsUpdateRequest`
for `appsettings.json`.
The `values` map contains typed changed values by colon-separated path.
For appsettings, `logging` and `openTelemetry` are also required. Full file
schema validation, read-only protection and atomic replacement remain in effect.

For the Serilog array fields, unknown entries, properties and JSON types
are preserved when submitted. Unchanged masked sensitive values are restored
from the prior file using the sensitive property name and mask, including when
entries move. A blank sensitive replacement is retained only when the original
entry has the same position and a unique unchanged name; ambiguous cases are
rejected. If different existing secrets share the same name and mask,
the request is rejected rather than guessing; replace the conflicting values
explicitly before editing that collection. Removing an entry or property
removes it, including its secret. Connection-string maps also preserve a
uniquely masked value when its key is renamed and reject ambiguous masks.
Removing a map entry or assigning null explicitly clears it.

## Compatibility and migration

The response envelope and field types are unchanged, but clients must handle
the Serilog arrays instead of assuming individual indexed scalar paths.
Deploy matching backend and frontend versions together. Existing configuration
files need no migration. Log-level changes retain their existing reload
behavior; restart the application after changing sinks, enrichers, filters,
destructuring or assemblies.
