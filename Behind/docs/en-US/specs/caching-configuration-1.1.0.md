# Caching configuration 1.1.0

Status: current. Supersedes [1.0.0](./caching-configuration-1.0.0.md) with
compatibility corrections to its documentation schema; runtime syntax is unchanged.

[JSON Schema](../../schemas/caching-configuration-1.1.0.schema.json).

`CachingConfiguration` still contains only `Provider` and `Key`. Provider defaults
to Memory; RedisCompatible resolves Key from sibling ConnectionStrings.
Missing configuration, null Caching, missing/null/blank Key and missing/null/blank
named connection preserve memory fallback. RedisCompatible without a connection
logs a startup warning. Configured service changes require restart.

Unlike the previous documentation schema, null Caching, null named connections
and preserved extension properties are accepted, matching existing binding and
Settings behavior. This does not make additional CachingConfiguration fields or
inline connection strings supported. Provider values remain Memory/RedisCompatible;
the Settings editor retains its existing field/path validation and secret handling.

The [configuration 1.0.0 runtime schema](./configuration-json-1.0.0.md) embeds the
same corrected shape and validates candidate documents before saving. Existing
PII/cache invalidation rules from 1.0.0 remain. No configuration migration.
