<!-- coderabbit-ai-generated:v1 -->

# Configuration JSON validation 1.0.2

Status: current. Supersedes [1.0.1](./configuration-json-1.0.1.md) with a field-specific Int64 correction. Previous specifications and schemas remain unchanged.

## Definition and coverage

[JSON Schema 2020-12](../../schemas/configuration-1.0.2.schema.json) describes the
JSON-backed configuration records, including nested records and converted values:
Caching, Database, Backend (accounts, comments, content restrictions, downloads,
scheduler), Frontend (site, sites, galleries, carousels, static files), Security
(JWT, elevation, data protection, magic links, OIDC), Email/authentication/templates,
Serilog minimum levels, OpenTelemetry exporters/logging, Forum and MarkdownViewer.
Named `$defs` describe records; derived values retain their actual converter shapes.
ConnectionStrings remain siblings of the Database and Caching sections.

## Runtime and compatibility

Common embeds the published schemas; `JsonSchemas` caches compiled validators and
never fetches schemas from user-provided URLs. `ConfigurationSchema` validates a
candidate document before the shared file writer replaces any bytes, inside its
existing write gate. Startup generation also validates before writing.
Generic Settings, email, logging/OpenTelemetry, OIDC, secret generation and
automatic magic-link rotation all use that shared writer. Settings APIs return
400 and log a value-free warning on schema rejection; internal writers throw
an explicit `ConfigurationSchemaException`. No rejected candidate changes a file.

Validation uses case-insensitive configuration property names without changing
stored property names or dictionary keys. The shared writer normalizes integral
MaxDecodedPixels values to Int64 JSON tokens before persistence. Missing properties keep existing defaults.
Optional records and values accept null where the existing binders do. Additional
properties stay allowed because the configuration system and module extensions
already preserve them. Other numbers remain JSON numbers, including negative values and fractions,
matching the generic Settings editor. MaxDecodedPixels alone requires an integer
within the signed Int64 range (-9223372036854775808 through 9223372036854775807).
Existing runtime fallback/clamping policies remain.
Domain validation, locale discovery, permissions, read-only rules, secret masking
and dedicated editor validation remain authoritative and are not replaced.

The schema checks known record shapes; it does not claim that every syntactically
valid string is a usable URI, secret, installed locale, cron expression or provider.
These contextual checks remain in existing converters and application validators.
Custom module sections outside this schema remain extensible; adding a known
record requires a schema definition and coverage tests in the same change.

## Compatibility evidence

`ConfigurationSchemaTests` checks generated files, reflection-based configuration
record/property coverage, accepted/rejected generic Settings inputs and unchanged
file contents on rejection. Existing Settings tests execute with validation enabled.
Comparison found SMTP Templates serialize as a dictionary, not an array; the
schema was corrected. Caching null sections and null named connections preserve
memory fallback; [caching 1.1.0](./caching-configuration-1.1.0.md) corrects the
older published documentation schema without changing configuration syntax.

Existing integral gallery pixel budgets remain compatible. A fractional or
out-of-range MaxDecodedPixels value previously allowed by schema 1.0.1 must be
replaced with an Int64 value or removed before saving. A pre-existing malformed
known field must be corrected before saving the whole document. Previous specifications remain intact.
Schema-only corrections are versioned; do not overwrite published contracts.

## Gallery decoded-image budget

`Frontend:Galleries:MaxDecodedPixels` in `frontend.json` is an Int64 integer setting with a default of 4,000,000 pixels. Missing and nonpositive values use that default through `GalleriesConfiguration.DecodePixelLimit`. It limits each decoded bitmap used for gallery thumbnails, covers and generated mosaics. Decoding selects dimensions suitable for the requested output and rejects a bitmap when the codec cannot fit it within the configured pixel budget. Identity orientation avoids an additional full-size bitmap.

`MaxImageBytes` continues to control compressed upload bytes independently; its existing 10 MiB default and nonpositive-value fallback remain unchanged. Increasing the pixel budget increases native memory use, including intermediate orientation and mosaic bitmaps. The pixel setting is a per-bitmap bound, not a total process-memory or concurrency limit.

This addition is compatible with existing configuration files: no migration is required and omitted values apply the safer default. Previously accepted images can fail thumbnail generation if their codec cannot decode within the budget. Operators can set a positive larger value after evaluating available memory. Generated startup configuration includes `MaxDecodedPixels`; the generic Settings editor exposes the new property through its existing configuration permissions. Values such as 4000000.0 and 4e6 are accepted as mathematical integers and written as 4000000, ensuring reload through the long configuration binder. Fractional values and values outside Int64 are rejected before any bytes change. Missing and nonpositive integral values retain the existing fallback. This corrects the earlier schema acceptance contract; it does not change the runtime pixel budget or other numeric fields.
