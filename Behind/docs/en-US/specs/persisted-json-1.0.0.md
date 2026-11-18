# Persisted JSON records 1.0.0

Status: current. These schemas document existing formats; no wire migration.

| Structure | JSON Schema 2020-12 | Validation before persistence |
| --- | --- | --- |
| LayoutConfiguration, SiteLayout, LayoutSection, LayoutWidget, LayoutBadge, LayoutAssignment | [layouts](../../schemas/layouts-1.0.0.schema.json) | LayoutService validates the sanitized candidate before atomic file replacement |
| ContactSubjects | [subjects](../../schemas/contact-subjects-1.0.0.schema.json) | Shared contact JSON converter, on read and write |
| ContactFieldSchema / ContactField | [fields](../../schemas/contact-fields-1.0.0.schema.json) | Shared contact JSON converter, on read and write |
| ContactFieldValues / ContactValue | [values](../../schemas/contact-values-1.0.0.schema.json) | Shared contact JSON converter, on read and write |
| ScheduleParticipants / participant entries | [participants](../../schemas/schedule-participants-1.0.0.schema.json) | ParseScheduleParticipants and ToJson |
| ScheduleParameters | [parameters](../../schemas/schedule-parameters-1.0.0.schema.json) | ParseScheduleParameters and ToJson |
| AuditAction and PropertyDifference payloads | [audit action](../../schemas/audit-action-1.0.0.schema.json) | Audit JSON converter and storage conversion before writing payloads |

Common's embedded `JsonSchemas` compiles validators once. Validation failures
never include payload values; schema names identify the failed format.
Typed request parsing and existing domain validation continue to reject invalid
values. Schemas describe structure, not cross-record permissions or business rules.
There is deliberately no duplication of dynamic installed-widget lists, locale
lookups, route uniqueness, identity references or cron/action rules in schemas.

Layouts retain their schemaVersion 0 upgrade and version 1 persistence, optimistic
revision checks, sanitization and deleted-layout protections. The published schema
describes the canonical camelCase output; the existing case-insensitive reader
remains compatible and saves canonical JSON.

Contact schemas accept both camelCase and PascalCase property names and optional
members supported by current serializers. Field enum tokens are the existing
`text`, `textarea`, `email`, `boolean` strings, not numeric enum values.
Schedule parameter values remain strings or null. Participants retain
case-insensitive property names and semantic kind parsing. Null root/default
handling and the existing FormatException for null participant entries remain;
schemas leave that semantic rejection to the parser rather than changing errors.
Audit simple actions remain plain stored names; payload actions retain their
`value` discriminator and arbitrary old/new difference values.

`PersistedJsonSchemaTests`, existing layout/contact/schedule/audit tests and
`JsonSchemaPostgresIntegrationTests` verify compatibility, rejected shapes,
unchanged files, converter round trips and actual PostgreSQL persistence.
PII-bearing contact/audit JSON is validated locally, not cached or logged.
