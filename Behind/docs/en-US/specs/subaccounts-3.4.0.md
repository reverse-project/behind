# Subaccount API 3.4.0

[JSON Schema](../../schemas/subaccounts-3.4.0.schema.json)

All routes require an authenticated, non-impersonated session. IDs identify users. Request and response bodies use the named schema definitions.

| Method and path | Input | Success |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| PUT /profile/subaccounts/{userId}/pin | `pinMutation` or `selfPinMutation` | 204 |
| POST /profile/subaccounts/{userId}/pin-proof | `pinProofRequest` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 sign-in tokens |
| GET /profile/subaccounts/candidates?query=…&page=1&pageSize=10 | — | 200 `candidateList` |
| POST /profile/subaccounts/{userId}/request | — | 204 |
| GET /profile/subaccounts/requests | — | 200 `invitationList` |
| POST /profile/subaccounts/requests/{requestId}/confirm | — | 204 |
| POST /profile/subaccounts/requests/{requestId}/decline | — | 204 |
| DELETE /profile/subaccounts/{userId} | — | 204 |

`canManageChildren` indicates whether this session can manage children. A primary can manage its linked profiles, including adult profiles. A switched adult can create, edit, invite and release children in its authorized family, but cannot create or edit other adults, change a child into an adult, or manage the primary's PIN. Directly signing in to a linked adult does not grant management of its primary's children. Authorization is recalculated from active links on each request. Impersonated sessions are excluded.

The `accounts` array contains management details, including birthday and email, for authorized profiles; for a switched adult it contains only children. `switchAccounts` omits private fields and groups destinations using `isChild`. The primary can switch to a linked profile without a PIN when none is set; a switched adult can also switch to an authorized child with no PIN. A stored destination PIN is always required. Other non-primary switches require a destination PIN. A valid adult PIN permits family management even when switching from a child. A child-originated adult session remains restricted from unrelated account-management operations. Switching invalidates the source session.

The `displayName` is required on create and update; stored response names may be blank. Birthdays use YYYY-MM-DD and cannot be in the future. A PIN is a digit string and preserves leading zeroes. Omit `pin` to preserve it on update, or set `clearPin` to true with an omitted or null `pin` to remove it. Providing a string PIN with `clearPin` returns 400.

Creating or changing a PIN requires a single proof credential. A primary can use the target's `currentPin`, its own `primaryPin` for a linked profile, its password, or a one-time email `magicToken`. A switched adult managing a child's PIN must use the adult's own PIN as `primaryPin`, password, or `magicToken`; the child's PIN alone is not sufficient. For creation with an initial child PIN, use the acting adult's PIN as `currentPin`, password or `magicToken`, and target the acting adult when requesting a `create` proof. A switched adult cannot set `primaryPin` on create. Linked users can set their own first PIN without proof and change or remove their own PIN with their current PIN. An unlinked switched adult without a PIN can set its first PIN using its own password or a `change` email proof for itself; subsequent changes and removal require its current PIN. Email proof is scoped to the actor, target and operation, expires, and requires the magic-link mode. Password proof requires password mode.

Both account feature flags must be enabled for linking routes. `canBrowseAndRelease` exposes browsing and release permission, `linkUnlinkEnabled` exposes invitation-sending permission, and `canRespondToInvitations` exposes incoming invitation actions. A primary may browse or release after switching back but must sign in directly to send invitations. A switched adult may send invitations on behalf of the family primary. Only a direct sign-in can view, accept or decline incoming invitations. Eligible candidates are active, have an email, have no profiles under them, and have no existing incompatible link. They may already be linked as a child to another adult. Invitations expose public labels, require recipient consent and are revalidated on acceptance.

Release requires a usable sign-in email, confirmed when configured. `canRelease` indicates eligibility. Release removes the primary's link, preserves account data and PINs, and invalidates affected switched sessions, including a switched caller. The client must clear a successfully released caller's switched session. Other direct sessions and independent family links remain. Permanent deletion of a primary with linked profiles returns 409 and `{"reason":"linkedSubaccounts"}`.

Invalid input returns 400; forbidden operations or invalid proof return 403; missing targets return 404; duplicate emails, conflicting link or PIN changes return 409; rate limiting returns 429.

`Backend.Accounts.SubaccountSwitch` independently disables the switch route with 404 while leaving management available. `GET /accounts/settings` reports `subaccountSwitch`. The PIN length is limited by `Security.Accounts.SwitchPinMaxLength`, a positive 32-bit integer defaulting to 128; the schema cannot express the deployment-specific limit. Invalid nonpositive configuration causes PIN requests to fail with 500.

## Compatibility and migration

Version 3.2.0 adds the required `canManageChildren` response field and extends the permissions and PIN-free destinations described above. Older clients may continue to use existing fields but cannot expose the new management controls. Apply all pending Core migrations before deploying, including `AddFamilySessionManager` and `VersionSubaccounts`. They preserve existing sessions and links, and record the authorized family for subsequent switches from child-originated sessions. The server rechecks both the originating link and the active family on each request. Session family-authority fields cannot be modified through the raw database editor. Downgrading removes that session state, so end affected switched sessions before rollback. The previous specifications and schemas remain available.

Version 3.3.0 removes the fixed schema PIN maximum and adds the independent switch gate. Existing configurations default to the previous behavior. Clients enforcing the old 128-character limit must update to use longer configured PINs. No new database migration is required.

Version 3.4.0 corrects the `selfPinMutation` proof schema and adds verified first-PIN setup for unlinked switched adults. SQLite installations must also apply the `RepairSubaccountVersions` migration, which normalizes version keys and enforces append-only updates.
