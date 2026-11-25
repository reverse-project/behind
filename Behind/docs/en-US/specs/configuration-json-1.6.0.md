<!-- coderabbit-ai-generated:v1 -->

# Configuration JSON validation 1.6.0

Status: current. This revision extends the [1.5.0 runtime configuration-schema contract](./configuration-json-1.5.0.md).

## Definition and coverage

[JSON Schema 2020-12](../../schemas/configuration-1.6.0.schema.json) describes the
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
the stored document or dictionary keys. Missing properties keep existing defaults.
Optional records and values accept null where the existing binders do. Additional
properties stay allowed because the configuration system and module extensions
already preserve them. Numbers remain JSON numbers, including negative values
and fractions, matching the generic Settings editor rather than inventing new
integer/range restrictions. Existing runtime fallback/clamping policies remain.
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

No file migration is necessary. A pre-existing malformed known field must be
corrected before saving the whole document. Previous specifications remain intact.
Schema-only corrections are versioned; do not overwrite published contracts.

## Audit search resource budgets

`backend.json` adds two optional numeric properties under `Backend`:

| Property | Default | Meaning |
| --- | --- | --- |
| `AuditSearchMaxEntries` | 10000 | Maximum audit entries scanned by a label search. |
| `AuditSearchMaxCandidates` | 10000 | Maximum candidate window retained for a requested label-search page. |

Both values are read through configuration for each search and clamped to at least
1 at runtime. A search exceeding its scan budget or requesting a page beyond the
candidate budget returns HTTP 400 rather than an incomplete successful result.
The schema accepts 32-bit signed integers for both budgets. Values that cannot
be read as integers at runtime use the corresponding default.

This is an additive configuration change: files omitting these properties retain
the defaults, and existing installations require no file migration. Startup
generation includes both defaults. Configuration 1.0.0 documents and schemas
remain unchanged; runtime validation now uses configuration 1.6.0. Deployments
that previously searched larger audit sets must narrow the search or deliberately
increase the configured budgets. This bounded-search behavior is a security repair.

## Gallery pixel budget

The signed Int64 validation and write normalization for `Frontend:Galleries:MaxDecodedPixels` carry forward from [configuration 1.0.2](./configuration-json-1.0.2.md). Missing or nonpositive values still use the existing default.

## Trusted forwarders

The optional `Backend:ForwardedHeaders` object in `backend.json` contains `Enabled` (default false), `KnownForwarders` (default empty string array), and `ForwardLimit` (positive integer, default 1). No migration is required. Settings reads and writes the section through `GET` and `PUT /settings/forwarded-headers` using the JSON fields `enabled`, `knownForwarders`, and `forwardLimit`. Changes take effect on reload.

## Account families

In `security.json`, `Security.Accounts.Subaccounts` defaults to true. Setting it to false disables family management and switching. `Security.Accounts.LinkUnlink` defaults to false. Enable both to allow invitations, confirmation and release. Existing files can omit the entire Accounts object. Disabling a feature preserves stored relationships.

`Backend.Accounts.SubaccountSwitch` in `backend.json` independently enables switching and defaults to true. Settings exposes it even when absent. Disabling it rejects switch requests with 404 and leaves family management and existing sessions unchanged. `GET /accounts/settings` exposes `subaccountSwitch`; the client reads it at startup and hides the switch control when disabled. Reload an open browser to refresh its control after a configuration change. The broader `Security.Accounts.Subaccounts` gate still applies.

The optional `Security.Accounts.SwitchPinMaxLength` setting accepts positive 32-bit integers and defaults to 128. The limit applies to both PINs on creation, to PIN updates and to switching. Settings rejects invalid values; a nonpositive manually configured value causes PIN requests to fail with 500 and logs an error. Lowering the limit preserves stored PINs, but switching with a longer PIN returns 400. Raise the limit to use a longer stored PIN again. Full sign-in remains available. No data migration is needed.

## Sign-in notification delivery deadline

`Security.SignInNotificationTimeoutMilliseconds` in `security.json` sets the
maximum time spent waiting for a post-commit sign-in security alert. It defaults
to 10000 milliseconds and accepts positive 32-bit integers. Timeout cancels
cancellable email providers and bounds waiting for providers that expose only
the basic email sender interface. Delivery failures and timeouts are logged;
they do not undo a committed sign-in. Existing files may omit the property,
and startup generation includes its default. No data migration is needed.
