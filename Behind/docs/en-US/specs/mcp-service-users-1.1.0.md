# MCP and service users 1.1.0

Status: current. This revision extends [MCP and service users 1.0.0](./mcp-service-users-1.0.0.md) with automatic tools for enabled modules. The matching [MCP JSON Schema](../../schemas/mcp-1.1.0.schema.json) describes the wire messages. The [service-user management schema](../../schemas/service-users-1.0.0.schema.json) and configuration schema remain unchanged.

## Identity and persistence

`ServiceUser` is a sealed `User` subtype, independent of its creator, like
`HumanUser`. Its Guid identity is an EF shadow key, not a property on the
domain record. Management projections expose the resource id and observed
credential version. Service users have their own roles, grants and restrictions.
Creation assigns the normal Users role, never the creator's roles.
No email, password, birthday, browser session or creator delegation is required.
Creation and changes are audited; timestamps come from the audit log.

Keys contain a service identity and 256 random bits. Only SHA-256 hashes are
stored. Verification uses fixed-time comparison. Creation and rotation reveal
the raw key once. A rotated key, disabled identity or soft-deleted identity
cannot authenticate. Restoring a deleted identity does not revive its old key.
Role and restriction changes apply on the next operation without reissuing a key.

## Settings

Both `Backend.Accounts.ServiceUser` and `Backend.Mcp.Enabled` must be true.
Both default to false and reload without restart. An absent configuration
preserves the disabled behavior of existing installations.
`Backend.Mcp.MaxRequestBytes` and `MaxResponseBytes` default to 1048576;
both must be positive integers. The response limit bounds the application API
body before it is wrapped in a JSON-RPC response.
`AllowedOrigins` defaults to an empty array. Requests without Origin are allowed;
a supplied Origin must match one configured origin exactly.

The backend Settings tab exposes these controls and service-user management.
Configuration.Read permits discovery. Configuration.Manage and User.Manage
permit creation, access changes, key rotation and enable/disable.
Configuration.Manage and User.Delete permit soft deletion. Restore and permanent
deletion additionally require developer access. Impersonated users cannot
modify service identities. Read-only configuration blocks all management writes.
An actor cannot assign grants they do not hold or manage an identity with
higher effective access. Keys are never returned in lists, access responses or
ordinary updates, and are not persisted in the browser.

## Management HTTP API

All responses are no-store. Protected endpoints reject missing authentication
with 401 and insufficient access with 403. Requests use camel-case JSON.

| Method and path | Request | Success |
| --- | --- | --- |
| GET `/service-users?deleted=false` | none | 200 `{users,canManage,canDelete,isDeveloper}` |
| GET `/service-users/{id}` | none | 200 `{id,version,name,enabled}` |
| POST `/service-users` | `{name}` | 201 `{user,apiKey}`, Location header |
| PUT `/service-users/{id}` | `{version,enabled}` | 204 |
| POST `/service-users/{id}/rotate` | `{version}` | 200 `{user,apiKey}` |
| GET `/service-users/{id}/access` | none | 200 existing UserAccessResponse |
| PUT `/service-users/{id}/access` | `{roleIds,grants,restrictions}` | 204 |
| POST `/service-users/{id}/delete` | `{version}` | 204 |
| POST `/service-users/{id}/restore` | none | 204 |
| POST `/service-users/{id}/hard/delete` | none | 204 |

`user` is `{id,version,name,enabled}`. Invalid names or selections return 400,
missing resources return 404 where lookup is required, and stale observed
versions return 409. Deleted listing is developer-only. The Users role remains
required. Access edits retain the existing no-escalation checks.
Permanent deletion returns 409 when retained audit history references the user.

## MCP Streamable HTTP

`/mcp` is a stateless Streamable HTTP endpoint. It requires
`Authorization: Bearer <service-key>` on every POST, GET and DELETE.
Keys are not accepted as browser JWTs or on ordinary application API routes.
Disabled MCP returns 404; missing/invalid keys return 401 with WWW-Authenticate;
invalid Origin returns 403. Use HTTPS outside local development.

POST requires Content-Type application/json and Accept listing both
application/json and text/event-stream. The server returns JSON, not SSE.
GET and DELETE return 405 because server streams and transport sessions are
not supported. There is no Mcp-Session-Id or persistent protocol state.
Supported protocol versions are 2025-11-25, 2025-06-18 and 2025-03-26.
Unknown initialization versions negotiate 2025-11-25; an unsupported explicit
MCP-Protocol-Version header returns 400.

Every POST contains one JSON-RPC 2.0 object, not a batch. Request ids are strings
or numbers and are preserved. Duplicate properties and invalid envelopes return
400/-32600. Malformed JSON returns 400/-32700. Oversized requests return 413.
Unsupported content types return 415 and unsupported Accept headers return 406.
Notifications with `notifications/` methods are accepted with 202 and no body;
they never execute tools.

Supported requests:

- `initialize`: requires protocolVersion, capabilities object and clientInfo object;
  returns protocolVersion, serverInfo, tools capability and instructions.
- `ping`: returns an empty result.
- `tools/list`: returns the two built-in tools and the tools for each currently
  enabled module.
- `tools/call`: requires `{name}`; `arguments` may be omitted for tools with no
  required arguments, such as `api_schema` and bodyless, parameterless module
  reads.
  Invalid arguments or an unknown or disabled module tool return -32602.
- Unknown methods return -32601. Protocol errors never execute an application command.

The built-in `api_schema` takes no arguments and returns the application OpenAPI
document. `api_request` takes `{method,path,query?,body?}`. Method is GET, POST,
PUT, PATCH or DELETE; path is a local application path with substituted route
parameters; query is an object of string values; body is JSON. Absolute URLs,
encoded paths, dot segments, embedded query strings and fragments are rejected.
Both built-in tools remain available as before.

### Automatically discovered module tools

For every installed and enabled module, MCP discovers eligible JSON operations
from the module's `ModuleEndpoint` route metadata and ASP.NET Core API Explorer
metadata. `tools/list` reads the currently enabled module catalog on every
request: a newly enabled module's tools appear without a new setting, and a
disabled module's tools disappear. No MCP-specific configuration toggle is
provided.

Each operation is named
`<module>_<lowercase HTTP method>_<route segments after the module prefix>`.
Path-parameter segments become `by_<parameter name>`; a module root operation
has just `<module>_<method>`. Examples include `forum_get_posts`,
`forum_get_posts_by_id`, `forum_post_posts`, `quotes_get`, `sport_get_teams`
and `markdownviewer_get_documents_by_path`.

Every operation has an object `inputSchema` describing only its route, query
and JSON-body inputs. A `route` object is present when the route has path
parameters, and is required in that case; its values are strings. Catch-all
route values may contain `/`. A `query` object describes API Explorer query
parameters using their API types (string, number or boolean). A `body` property
uses the request type's JSON schema from the application's configured JSON
serializer. Required fields in the schema follow the endpoint's API binding.
An operation without one of these input kinds omits the corresponding property.

Only ordinary JSON operations are exposed. This includes public and management
operations, moderation, soft delete, restore and permanent deletion; their
existing authorization rules remain in force. Multipart/file uploads and
binary downloads are excluded. API Explorer response metadata identifies JSON
operations; endpoints must declare their actual JSON response DTO with
`.Produces<T>()` and use non-JSON response metadata for binary responses.
Bodyless successful operations, such as lifecycle actions returning 204, remain
eligible through their success-status metadata (for example, `.Produces(204)`);
they do not require a JSON DTO. Their successful tool result contains the status
and an empty body string. Application failures remain tool errors and are not
converted into successful results.

Tool discovery is not filtered by the service user's grants, so the schema
describes the current enabled API surface rather than an authorization snapshot.
Each invocation goes once through the normal application routing and middleware,
including module availability, authentication, authorization, locale, age and
deletion checks, with the service user's own identity. A caller without the
required live permission receives the ordinary denied outcome. Disabling a
module removes its tools; calling a stale or unknown tool name returns -32602.
Mutations are dispatched once and are never automatically retried.

Dispatch is in-process. No authorization result or command response is cached.
No external HTTP request or arbitrary headers are accepted.
X-Content-Locale and Accept-Language are preserved. Interactive
sign-in/session/profile/subaccount/impersonation APIs and MCP or service-user
management cannot be invoked through MCP tools. Redirects and binary responses
are not supported.

`api_request` and module tool results both contain
`content:[{type:"text",text:"..."}]` and `isError`. The text is JSON
`{status,body}`, containing the application HTTP status and response body as a
string. For example, a bodyless 204 success has a text value of
`{"status":204,"body":""}`, not an empty content array. HTTP errors set
`isError` without disguising failures as successful operations. If an application
response exceeds its configured limit, status is 502 and the error warns that a mutation
may already have completed. There are no automatic retries. Caller cancellation
propagates to application operations. Audit attribution uses the independent
service identity.

## Migration and compatibility

Version 1.1.0 is additive to the MCP tool surface: `api_schema`, `api_request`,
service-user management, authentication, transport and configuration are
unchanged. Clients must treat `tools/list` as dynamic and must not assume it
contains exactly two tools. Existing service-user management clients and their
1.0.0 schemas remain compatible. Deploy the application version that includes
module routes and their API Explorer metadata to expose those operations.
No database migration, new setting or external service is required.

Run the existing migrations for the installation's flattened SQLite/PostgreSQL
context before enabling MCP. They add service identities and extend shared
user access/audit/authorship references to the User hierarchy while preserving
existing human users and memberships. TPC polymorphic references cannot have a
database foreign key to one concrete user table; application identity checks
remain authoritative. Human-only sign-in credentials remain human-only.
Deploy backend and frontend together. No package or external service is required.
