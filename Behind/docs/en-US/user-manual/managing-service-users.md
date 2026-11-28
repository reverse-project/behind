# Service users and AI agents

Open **Administration > Settings > backend.json**. Enable **Service user
authentication** under Accounts and **MCP server**. Existing installations
default to disabled. Changes reload without a restart.

Create a named service user in the service-user editor. It is a separate user,
not your account: it has its own roles, grants and restrictions and never
inherits your permissions. Use its access editor to assign only the permissions
the agent needs. You cannot grant permissions you do not hold.

Copy the bearer key shown after creation; it is displayed once. Configure the
agent's Streamable HTTP MCP connection to `https://your-host/mcp`, with
`Authorization: Bearer <key>`. Keep it secret. The `api_schema` tool discovers
the application's JSON APIs; `api_request` calls them with the service user's
own access. MCP also discovers JSON operations from every installed, enabled
module on each `tools/list` request. For example, a forum can expose
`forum_get_posts` and `forum_post_posts`; the tool schema describes route,
query and JSON-body inputs. The tools are not hidden based on the service user's
grants, but every call is checked using its current permissions and the normal
application middleware. This includes module management and moderation actions
where that identity has access. Disabling a module removes its tools; calling a
stale or unknown tool returns -32602. Interactive accounts and binary transfers
are not supported. Calls without required arguments can omit `arguments`. As
with `api_request`, module tool text contains JSON `{status,body}`; for a
bodyless response the `body` is an empty string.

Disable a user to revoke access temporarily or rotate its key to replace a
credential. Soft deletion revokes access. Developers can list deleted identities,
restore them and delete them permanently; restoration does not revive old keys.
Disabling either MCP setting blocks the endpoint without removing users.

Management requires Configuration permissions and User.Manage or User.Delete.
Read-only configuration blocks changes. Origin-less agent requests are allowed;
browser Origin values must be explicitly listed under MCP AllowedOrigins.
Request and API response sizes are configurable. Never blindly retry a mutation
when an oversized response reports that the operation may have completed.
