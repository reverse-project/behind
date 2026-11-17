# Caching configuration 1.0.0

Status: current. Schema: [caching-configuration-1.0.0.schema.json](../../schemas/caching-configuration-1.0.0.schema.json).

## Contract

The optional `caching.json` file contains a `Caching` object with exactly two
configuration properties:

- `Provider`: `Memory` (default) or `RedisCompatible`. Runtime values use the
  `CachingProvider` record variants and type/JSON converters, like database providers.
- `Key`: optional string or null. It names a `ConnectionStrings` entry; it is not
  an endpoint, credential or inline connection string.

The sibling `ConnectionStrings` object maps names to strings. `startup create`
generates `Provider: Memory`, `Key: CachingConnection` and an empty
`ConnectionStrings:CachingConnection`. `RedisCompatible` uses the selected entry
with the Redis-compatible client and supports Valkey. Missing file, provider or
connection configuration uses memory only; a selected Redis-compatible provider
without a usable named connection logs a startup warning. An invalid provider
fails startup. An unreachable configured endpoint retains normal client/cache
failure diagnostics rather than silently changing the selected provider.

## Settings UI and security

The existing Settings API and form list `caching.json` as a tab. Provider uses
a validated selector; Key is editable. Named connections are password fields
and never returned unmasked. Empty password input preserves the stored secret;
clearing Key disables the distributed backend after restart. Configuration read
and management grants and read-only mode apply unchanged. Provider and connection
changes require a restart; the running cache service is not hot-swapped.

No PII, credentials, user text, sessions or authorization results may be cached.
Commands always execute. Existing aggregate caching uses committed audit revisions,
transaction bypass and database/model isolation; expiration alone is not safe
invalidation. Use private authenticated/TLS-protected Valkey connections.

## Compatibility and migration

This is the first published caching configuration contract. Existing installations
without the file remain compatible and use memory. Run `startup create
--skip-existing`, mount the generated file next to the executable and restart.
The compose example mounts it into the web host.

The earlier development-only `Backend:Caching` shape is not read. Move its inline
connection to `ConnectionStrings` in `caching.json`, select it with `Caching:Key`,
and remove old `ConnectionString`, `InstanceName` and `QueryExpiration` properties.
`CachingConfiguration` must not grow inline connection or cache-policy fields.
