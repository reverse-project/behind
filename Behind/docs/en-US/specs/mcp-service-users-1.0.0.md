# MCP and service users 1.0.0

Status: current. Schemas: [management API](../../schemas/service-users-1.0.0.schema.json),
[JSON-RPC messages](../../schemas/mcp-1.0.0.schema.json),
[configuration](../../schemas/configuration-1.8.0.schema.json).

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

The User Administration page exposes service-user management in its Service users tab. The backend Settings tab continues to expose the configuration controls.
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
- `tools/list`: returns the two tools below.
- `tools/call`: requires `{name,arguments}`; invalid arguments return -32602.
- Unknown methods return -32601. Protocol errors never execute an application command.

`api_schema` takes no arguments and returns the application OpenAPI document.
`api_request` takes `{method,path,query?,body?}`. Method is GET, POST, PUT, PATCH
or DELETE; path is a local application path with substituted route parameters;
query is an object of string values; body is JSON. Absolute URLs, encoded paths,
dot segments, embedded query strings and fragments are rejected.

Application requests execute in-process through the normal routing,
authentication, authorization, module availability, locale, age and deletion
middleware with the service user's own identity. No authorization result or
command response is cached. No external HTTP request or arbitrary headers are
accepted. X-Content-Locale and Accept-Language are preserved.
Interactive sign-in/session/profile/subaccount/impersonation APIs and MCP or
service-user management cannot be invoked through this tool. Only JSON API
responses are supported; binary uploads/downloads and redirects are not supported.

Tool results contain `content:[{type:"text",text:"..."}]` and `isError`.
Text is JSON `{status,body}` containing the application status and body text;
HTTP errors set isError without disguising failures as successful operations.
If an application response exceeds its configured limit, status is 502 and
the error explicitly warns that a mutation may already have completed.
There are no automatic retries. Caller cancellation propagates to application
operations. Audit attribution uses the independent service identity.

## Migration and compatibility

Run the new migrations for the installation's flattened SQLite/PostgreSQL
context before enabling MCP. They add service identities and extend shared
user access/audit/authorship references to the User hierarchy while preserving
existing human users and memberships. TPC polymorphic references cannot have
a database foreign key to one concrete user table; application identity checks
remain authoritative. Human-only sign-in credentials remain human-only.
Deploy backend and frontend together. No package or external service is required.
