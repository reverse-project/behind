# Behind OIDC sign-in 1.0.0

Status: implemented. This additive public contract follows Semantic Versioning 2.0.0 independently of application and Auditor versions. Previous authentication endpoints and response formats are unchanged. The machine-readable [handoff schema](../../schemas/oidc-sign-in-1.0.0.schema.json) describes the new provider list and redemption request.

## Configuration

Add this section under `Security` in `security.json`, supply the real client secret through protected installation configuration, and restart:

```json
{
  "Oidc": {
    "TicketLifetime": "00:02:00",
    "AuthenticationTimeout": "00:10:00",
    "MaxPendingTickets": 1000,
    "Providers": [
      {
        "Id": "company",
        "DisplayName": "Company account",
        "Enabled": true,
        "Authority": "https://identity.example.org/realms/company",
        "ClientId": "behind",
        "Scopes": ["openid", "profile", "email"],
        "AutoCreateUsers": true,
        "LinkByVerifiedEmail": true,
        "RoleClaimType": "realm_access.roles",
        "RoleMappings": {"content-editors": ["Editor"]},
        "AutoCreateRoles": true,
        "AutoCreateRolePrefix": "company:"
      }
    ]
  }
}
```

Add more provider objects for multiple independent providers. IDs are unique, case-sensitive ASCII letters/digits/underscore/hyphen identifiers; case-only duplicates are refused. Keep an ID stable once identities are linked. Empty providers disable OIDC without changing existing authentication. Enabled providers require a display name, HTTPS authority without credentials/query/fragment, client ID, nonempty role claim type and scopes containing `openid`. Invalid configuration fails startup explicitly. `ClientSecret` is optional for public clients and masked by Settings. Do not put real secrets in source control.

Defaults: enabled provider; scopes `openid`, `profile`, `email`; automatic user creation true; email linking false; role claim `roles`; no mappings or automatic role creation. The automatic role prefix defaults to `oidc:<provider-id>:`. Lifetime and capacity values must be positive. All OIDC settings are startup-only.

Configure the provider to include `email`, `email_verified`, optional `name` and the selected role claim in its ID token. Behind does not fetch the UserInfo endpoint or infer verification from an email claim alone.

## Browser protocol and endpoints

1. `GET /sign/in/oidc/providers` returns HTTP 200 and `[{ "id": "company", "displayName": "Company account" }]`. No credentials, claims or role mappings are exposed. Disabled global sign-in or missing JWT configuration returns an empty array.
2. Navigate the browser to `GET /sign/in/oidc/company`. HTTP 302 starts authorization code flow with PKCE. A Secure, HttpOnly, SameSite=None, per-flow cookie binds the browser. Unknown/disabled providers or disabled sign-in return 404; non-HTTPS requests return 400.
3. Register the exact backend callback `https://<backend>/sign/in/oidc/callback/company` at the provider. Standard ASP.NET Core OIDC middleware validates protected state, correlation, nonce, signature, issuer, audience and token lifetime and exchanges the code. Provider credentials and tokens stay server-side; tokens are not saved.
4. Successful validation redirects only to configured `Frontend:Url#/signin?oidc=<opaque-ticket>`. Failure redirects to `#/signin?oidcError=failed`; provider failure details are not exposed.
5. `POST /sign/in/oidc/redeem` with JSON `{ "ticket": "<opaque-ticket>" }` and the original browser cookie redeems exactly once. The UI removes the ticket from its hash route. Invalid, expired, replayed or unbound tickets return 401 without a session; disabled sign-in or absent JWT settings returns 404. Invalid request shape returns 400.

Both frontend and backend require HTTPS. Serve them under the same origin; cross-origin deployments require an explicit credentialed CORS policy and browsers allowing the binding cookie. Handoff storage is bounded and process-local; use sticky routing on multiple instances. Restart or routing to another instance invalidates the pending handoff, not persisted identities. No access/refresh token is carried in a URL. Responses use `Cache-Control: no-store`.

## Accounts and authorization

First use requires a syntactically valid email and the provider's `email_verified=true` assertion. Automatic creation also requires `Backend:Accounts:SignUp=true`; standard user-role initialization and moderated approval apply. Existing-email linking requires `LinkByVerifiedEmail=true` and confirmation of that exact local address. Opt-in linking grants trust in that provider's email verification and account lifecycle: enable it only for providers you trust to protect verified addresses.

The persistent identity is the provider ID, validated issuer and subject, not email. Once linked it cannot be silently rebound to another user, including when the linked user is soft-deleted or the provider email changes. Bindings survive restart; permanently deleting a user removes its associated infrastructure records. Behind's existing email-confirmation, approval, ban and two-factor gates run before a session is issued. OIDC second-factor tickets retain the originating provider and do not depend on Password/MagicLink being enabled. A removed provider cannot complete outstanding OIDC second-factor challenges after restart.

Repeated string claims, JSON string arrays and dotted JSON paths such as `realm_access.roles` are supported for roles. Invalid role value shapes fail explicitly. Role mapping is exact and case-sensitive for external names; local roles follow existing case-insensitive lookup. Each mapped target must already exist and be active. Unmapped roles are ignored unless automatic creation is enabled; then their prefixed names create/reuse ordinary nondeveloper roles without grants. Privileged automatic targets are refused. Explicit mappings may intentionally target privileged roles: treat configuration as security-sensitive.

Synchronization replaces only the current provider's role contribution. Manual assignments and other providers' contributions remain. A missing external role removes a formerly OIDC-owned membership when no other source needs it. Synchronization occurs at account resolution, not continuously; provider disable/removal does not itself revoke prior roles or existing sessions. Use administration to revoke access/sessions immediately.

## Responses, persistence and migration

Successful redemption returns the existing sign-in JSON `{accessToken, expires, refreshToken}` or the existing second-factor challenge `{ticket, methods, setupRequired, function}`. Pending approval, unconfirmed email, ban and unavailable two-factor responses retain existing HTTP 403 contracts. Account resolution failures return 401; persistence/configuration failures are logged and surfaced as server errors, never success-shaped defaults.

Apply the supplied Core SQLite/PostgreSQL and flattened-host migrations before enabling providers. Infrastructure identity/source records are read-only in raw database management; local users and roles keep their existing management lifecycle. New tables do not rewrite previous users, passwords, emails or roles. Rolling back application code is safe while providers remain disabled; do not drop identity bindings unless intentionally abandoning trusted associations. No Auditor engine or data format changes are introduced.
