---
name: manage-caching
description: 'Configure and maintain HybridCache with memory or Valkey, the caching.json Settings UI, secure query caching and cross-host invalidation. Use when adding or changing caching, cache providers, cache keys, invalidation, cached database aggregates or caching configuration.'
---

# HybridCache and Valkey

Follow `csharp-code-standards` and the repository `AGENTS.md`.

## Configuration contract

- `Behind.Common.Configuration.CachingConfiguration` contains exactly `Provider`
  and `Key`, following `DatabaseConfiguration`. Keep provider/key domain records
  with discoverable type converters and JSON converter factories in Common.
- `caching.json` contains `Caching` and sibling `ConnectionStrings` sections.
  `Memory` is the default; `RedisCompatible` resolves the optional Key through
  `IConfiguration.GetConnectionString`. Missing/blank key or connection means
  memory-only with a startup warning. Invalid providers fail explicitly.
- Do not add inline connection strings, namespaces, expirations or other policy
  fields to `CachingConfiguration`. Never place caching settings in backend.json.
- Preserve `startup create` generation, compose mounts and every Core-based host.
  A missing file stays compatible with existing memory-only installations.
- Use the existing Settings form: provider options and validation, editable Key,
  masked ConnectionStrings, unchanged Configuration.Read/Manage and read-only
  checks. Blank secret inputs preserve secrets; clearing Key disables L2 after
  restart. Configuration changes require restart, not hot-swapping clients.
- Keep both documentation languages, specifications and schemas current. Current
  contract: `Behind/docs/en-US/specs/caching-configuration-1.1.0.md`.

## Safe query caching

- Never cache PII, credentials, sessions, authorization results, user-authored text
  or entire rows/command responses. Public content is not necessarily PII-free.
  Commands always execute validation, authorization, writes and auditing.
- The current allowlist is numeric table-count aggregates in
  `RawDatabaseQueries.ListTables`; metadata and authorization remain uncached.
  Do not extend the allowlist without checking payloads and all write paths.
- Use fixed-shape, bounded-cardinality keys; never raw searches, identities,
  secrets or attacker-controlled input. Partition by database/provider/model.
- Existing aggregates include the database's committed audit-row count in keys.
  Recheck before publishing; do not share scoped DbContext work in cache factories.
  Bypass reads and writes for explicit/ambient transactions or pending changes.
  Old readers may populate only old generations, never the current key.
- TTL bounds obsolete entry retention, not consistency. Cross-host L1 entries
  cannot rely on remote RemoveAsync/tag invalidation alone. Keep audited writes
  atomic and flush caches after unaudited SQL, database restore or audit pruning.
- Keep HTTP no-store and permission checks intact. Use private authenticated/TLS
  Valkey, even for PII-free operational metadata.

## Tests and verification

- Follow `CommonWebApi/CachingIntegrationTests` and
  `CoreDatabase/RawDatabaseCachingTests` in `Behind/tests/Behind.Tests`.
- Run disposable PostgreSQL and Valkey via Testcontainers, tag
  `Category=Integration`, read images from testsettings JSON, and `await using`
  containers. Missing engines fail explicitly; do not silently skip.
- Verify actual cache hits using SQL tracing, named-key resolution, memory
  fallback, provider/key conversion, cross-host invalidation, committed writes,
  rollback/transaction bypass, database isolation and secret masking.
- From `Behind/`, with the root README's Docker/Podman environment:
  `dotnet test tests/Behind.Tests/Behind.Tests.csproj --filter "FullyQualifiedName~Caching|FullyQualifiedName~Settings|FullyQualifiedName~StartupSetupTests|FullyQualifiedName~RawDatabase"`
- Update dependency licenses, changelog, generated defaults and related docs.
