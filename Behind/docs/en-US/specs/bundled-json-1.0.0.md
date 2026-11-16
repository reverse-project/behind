# Bundled frontend JSON 1.0.0

Status: current. This first published contract describes existing bundled static
records, not configuration, import/export documents or API requests/responses.
It adds build-time regression validation, not runtime request validation.

## Schemas and files

All schemas use JSON Schema Draft 2020-12 and are embedded in `Behind.Common`.

| Bundled file, relative to a project's `Frontend` directory | Schema |
| --- | --- |
| `changelog.json` | [changelog-1.0.0.schema.json](../../schemas/changelog-1.0.0.schema.json) |
| `licenses.json` | [licenses-1.0.0.schema.json](../../schemas/licenses-1.0.0.schema.json) |
| `locales/*.json`, `modules/<key>/locales/*.json` | [locale-1.0.0.schema.json](../../schemas/locale-1.0.0.schema.json) |

`BundledJsonSchemaTests` discovers every applicable source file across the core
and module frontend directories and evaluates it against the embedded schema.
Malformed fixtures cover required fields, types and dictionary values.

## Changelog

A nonempty array of release objects. Each release requires nonblank string
`version` and `date`, and a `changes` array. Each change requires nonblank string
`type` and `text`. An empty changes array remains valid for a development release.
No semantic-version or date-format restriction is added: the release-notes
generator accepts nonblank strings. Change types are not an enum: unknown types
are displayed literally by the frontend and supported by release-note generation.

Existing changelog tests additionally enforce the current version, unique
versions and nonempty changes for previous releases.

## Licenses

A nonempty array. Each record requires nonblank string `name`, `version`,
`license`, `copyright`, `url`, `scope` and `text`. `scope` is exactly `frontend`,
`server` or `development`. The optional `note` is a string or null. Version
strings need not be semantic versions; URLs and license-text paths retain their
existing string representation without new format or path restrictions.

Existing license tests remain responsible for package coverage, unique
name/version pairs, GPL-compatible license identifiers and existing license-text
resources. These cross-file and legal checks are not replaced by a schema.

## Core and module locales

Both use the same object schema. `messages` is required and maps arbitrary keys
to string translations. Empty dictionaries and empty translations are valid;
dotted keys, Unicode and positional placeholders such as `{0}` are preserved.
Nested message objects, arrays, numbers, booleans and null translations are invalid.

Optional `name` and `flag` are strings or null. Missing, null, empty or blank
metadata remains valid: the locale catalog falls back to the file's locale code
for an absent/blank name and to no flag for absent/blank flag metadata. Module
bundles need not contain a flag. The file name supplies the locale code; its
validation and translation completeness remain separate concerns.

## Compatibility and migration

All three schemas allow additional record properties, matching consumers that
ignore unknown metadata. They describe complete bundled records, not every
malformed input that a defensive frontend happens to tolerate. In particular,
locale values are string dictionaries and changelog release fields satisfy the
release-notes generator's requirements.

No bundled data changes or migration are required. No schema-version field is
inserted into existing arrays or locale objects; the version belongs to the
schema file and this specification. Future contract changes must publish a new
semantic version while retaining these schemas and specification.
