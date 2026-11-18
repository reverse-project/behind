# Behind OIDC sign-in 2.0.0

Status: implemented. This specification follows Semantic Versioning 2.0.0 independently of application and Auditor versions. It supersedes [1.1.0](./oidc-sign-in-1.1.0.md), which remains historical. Version 2.0.0 changes local role matching to case-sensitive exact matching; handoff and settings JSON shapes remain unchanged. The [JSON Schema](../../schemas/oidc-sign-in-2.0.0.schema.json) covers the handoff and settings payloads.

## Global account-method switch

In `backend.json`, `Backend:Accounts:Oidc` enables or disables **all** OIDC providers, independently of Password, MagicLink and other methods:

```json
{"Backend":{"Accounts":{"Oidc":true}}}
```

The switch defaults to true for compatibility with existing configured providers. With no providers, OIDC remains unavailable regardless of the switch. Settings exposes the switch under **backend.json > Backend > Accounts > Oidc**, including when absent from the file. Saving adds the missing boolean. It reloads through the normal configuration watcher without a restart; file-watch polling can delay propagation by a few seconds. `GET /accounts/settings` includes the `oidc` boolean.

While false, the public provider list is empty; starting sign-in and redeeming pending handoff tickets return 404; provider callbacks redirect to a generic failure; outstanding OIDC second-factor challenges are rejected. No new OIDC session is issued. This does not disable password authentication or revoke existing sessions or roles.

## Provider editor and persistence

The **security.json** Settings tab retains its generic security fields, Generate and Rotate actions, and additionally has a dedicated **OpenID Connect providers** form. Add, edit or remove providers, enable individual providers, set scopes, account-linking and creation policy, role-claim paths, explicit role mappings and automatic role creation. Timeouts and pending-ticket capacity are editable there too. The global switch is not stored under `Security:Oidc`.

- `GET /settings/security.json/oidc` requires `Configuration.Read` and returns the configuration shape below, using camelCase JSON names. Every provider's `clientSecret` is empty, `hasClientSecret` indicates whether a secret exists, and `clearClientSecret` is false. No actual or masked client-secret content is returned.
- `PUT /settings/security.json/oidc` requires `Configuration.Manage` and replaces the provider list. Empty `clientSecret` preserves a prior secret by exact stable provider ID, never by row position. A nonempty value replaces it; `clearClientSecret=true` clears it. Clearing and supplying a replacement simultaneously is invalid. Stored and unknown unrelated JSON properties are preserved, including unknown properties of retained providers. Secret status/clear UI flags are never persisted.
- Valid writes return 204. Invalid providers, duplicate IDs, role mappings, scopes or limits return 400 without writes. Read-only configuration returns 403; missing security.json returns 404. Unauthorized requests return 401/403 according to existing authorization. Reads/writes use `Cache-Control: no-store`; errors propagate through existing error handling, and logs contain no secrets.
- Provider changes and limits require a restart. Saving the editor does not restart the process or register new authentication schemes dynamically. Unsaved generic security-field edits and provider edits are independent. Changing an existing provider ID creates a different identity namespace; keep IDs stable.

## Provider configuration

Place this under `Security` in `security.json`, supply the real client secret through protected installation configuration, and restart:

```json
{
  "Oidc": {
    "TicketLifetime": "00:02:00",
    "AuthenticationTimeout": "00:10:00",
    "MaxPendingTickets": 1000,
    "Providers": [{
      "Id": "company", "DisplayName": "Company account", "Enabled": true,
      "Authority": "https://identity.example.org/realms/company", "ClientId": "behind",
      "Scopes": ["openid", "profile", "email"],
      "AutoCreateUsers": true, "LinkByVerifiedEmail": true,
      "RoleClaimType": "realm_access.roles",
      "RoleMappings": {"content-editors": ["Editor"]},
      "AutoCreateRoles": true, "AutoCreateRolePrefix": "company:"
    }]
  }
}
```

Multiple objects configure independent providers. IDs are unique ASCII letters/digits/underscore/hyphen identifiers; case-only duplicates are refused. Enabled providers require a display name, HTTPS authority without credentials/query/fragment, client ID, nonempty role-claim type and scopes containing `openid`. Invalid configuration fails startup explicitly. `ClientSecret` is optional for public clients; do not commit real secrets. Individual provider `Enabled` defaults to true; scopes default to `openid`, `profile`, `email`; automatic user creation defaults to true; email linking and automatic role creation default to false; role claim defaults to `roles`; no mappings. Default automatic role prefix is `oidc:<provider-id>:`. Lifetimes and capacity must be positive.

Configure the provider to include `email`, `email_verified`, optional `name` and the selected role claim in its ID token. Behind does not fetch UserInfo or infer verification from an email alone. Backend and `Frontend:Url` require HTTPS when providers are configured, even when the global method switch is currently off, so they can be enabled without restart.

## Browser sign-in

`GET /sign/in/oidc/providers` returns enabled `[{id, displayName}]` without credentials or mappings. Disabled global sign-in, disabled OIDC or absent JWT settings returns an empty list. Browser navigation to `GET /sign/in/oidc/<id>` starts authorization code flow with PKCE (302); unknown/disabled providers return 404, non-HTTPS requests 400. A Secure, HttpOnly, SameSite=None, per-flow cookie binds the browser. Register the exact `https://<backend>/sign/in/oidc/callback/<id>` callback at the provider.

Standard ASP.NET Core middleware validates protected state, correlation, nonce, signature, issuer, audience and lifetime, then exchanges the code. Tokens remain server-side and are not saved. Success redirects only to configured `Frontend:Url#/signin?oidc=<opaque-ticket>`; failure to `#/signin?oidcError=failed`. The frontend removes the ticket from its URL and posts `{ticket}` to `/sign/in/oidc/redeem` with the original cookie. Tickets are single-use, browser-bound, bounded by configured lifetime/capacity, and process-local. Invalid/expired/replayed/unbound tickets return 401, malformed request 400. No access/refresh tokens appear in URLs. Use same-origin hosting; other origins require explicit credentialed CORS and allowed cookies. Multiple instances require sticky routing; restarting invalidates pending handoffs.

## Accounts, roles and responses

First sign-in requires a valid email and `email_verified=true`. Automatic creation also requires `Backend:Accounts:SignUp`; standard Users role assignment and moderated approval apply. Existing-email linking additionally requires `LinkByVerifiedEmail=true` and confirmation of that exact local address. Enable linking only for trusted providers. Later logins use persisted provider/validated issuer/subject, not changed email. Soft-deleted users cannot be silently rebound; permanent deletion removes associated infrastructure records.

Roles support repeated strings, JSON string arrays and dotted JSON paths. Invalid shapes fail explicitly. External mapping keys and local role names match exactly, including case. Explicit targets must already exist and be active; configured mappings may intentionally grant elevated roles. Unmapped roles are ignored unless automatic creation is enabled; then prefixed ordinary zero-grant, zero-restriction, nondeveloper roles are created/reused, and privileged automatic targets are refused. Synchronization replaces only the current provider's contribution, preserving manual memberships and other providers. It occurs at account resolution, not continuously; removing providers does not revoke previous roles/sessions.

Before upgrading, check each configured mapping target and automatic-role prefix and claim against the exact case of its intended local role. A mismatched explicit target now fails sign-in; an unmapped automatic role may create a distinct case-sensitive role instead of reusing the old one. Correct configuration and provider claims before the next sign-in, and review provider-owned memberships after migration. Existing role IDs and memberships are not renamed or merged.

Behind's confirmation, approval, bans and two-factor gates precede session creation. OIDC second-factor tickets retain the provider and do not depend on Password/MagicLink being enabled. Successful redemption uses existing `{accessToken, expires, refreshToken}` or `{ticket, methods, setupRequired, function}` responses; existing approval/confirmation/ban/unavailable-factor HTTP 403 responses remain unchanged. Account resolution failure returns 401; configuration/persistence failures are server errors.

## Compatibility and migration

Apply the 1.0.0 Core SQLite/PostgreSQL migrations through the existing host migrators before enabling providers. Identity/source records remain read-only in raw database management, with users and roles retaining existing lifecycle controls. Version 1.1.0 adds no database migration and rewrites no existing credentials. Existing installations without `Backend:Accounts:Oidc` retain enabled-method behavior. No Auditor contracts change.
