# Subaccount API 2.3.0

[JSON Schema](../../schemas/subaccounts-2.3.0.schema.json)

All routes require an authenticated session. IDs identify users. Request and response bodies use the named definitions in the schema.

| Method and path | Input | Success |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| PUT /profile/subaccounts/{userId}/pin | `pinMutation` (primary), `selfPinMutation` (linked subaccount) | 204 |
| POST /profile/subaccounts/{userId}/pin-proof | `pinProofRequest` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 with the usual sign-in tokens |
| GET /profile/subaccounts/candidates?query=…&page=1&pageSize=10 | — | 200 `candidateList` |
| POST /profile/subaccounts/{userId}/request | — | 204 |
| GET /profile/subaccounts/requests | — | 200 `invitationList` |
| POST /profile/subaccounts/requests/{requestId}/confirm | — | 204 |
| POST /profile/subaccounts/requests/{requestId}/decline | — | 204 |
| DELETE /profile/subaccounts/{userId} | — | 204 |

The primary account creates new users and edits their display name, birthday, child flag and PIN. Birthdays use YYYY-MM-DD and cannot be in the future. Omitting the PIN on update preserves it unless `clearPin` is true. To remove the PIN, set `clearPin` to true and omit `pin` or pass null. A string-valued `pin` with `clearPin` set to true returns 400. A PIN is a string of digits, including any leading zeros.

PIN inputs have a maximum of 128 characters. Administrators can lower this limit in [`Security.Accounts.SwitchPinMaxLength`](./configuration-json-1.4.0.md); an input longer than the configured limit returns 400.

The optional `primaryPin` field on create sets the primary account’s first switching PIN. Omit it to skip setup. The `pin` field belongs to the subaccount. Eligible family sessions can return to the active primary account using its PIN. Without one, full sign-in is required. Direct subaccount sign-ins cannot switch. The family response includes `primaryHasPin` (false for direct subaccount sign-ins). The primary appears in `accounts` only when eligible for PIN switching. Only the primary can see birthdays and email addresses. Successful switching invalidates the source session.

Invalid input returns 400, a forbidden switch or missing or incorrect required PIN returns 403, and an unavailable target returns 404. Duplicate email, attempting to replace an existing primary PIN during creation, or losing a concurrent first-PIN insert during update returns 409. Switch rate limiting returns 429. Before deployment, apply the Core migration for SQLite or PostgreSQL. It copies existing hashes unchanged to their owners and leaves primary PIN setup optional.

Rolling back restores subaccount hashes to their links. The 1.0 schema cannot store primary PINs, so users must set those again after a rollback and subsequent upgrade.


## Linking existing users

Both security account flags must be enabled. Disabled routes return 404. The family response includes `linkUnlinkEnabled` for the current session. A pending invitation confers no family access and cannot be accepted while either flag is disabled. Re-enabling both allows the recipient to review an outstanding invitation again.

A directly signed-in primary may browse eligible users by display name. Candidates must be active, have an email and have neither a parent nor children. Candidate and invitation results expose public labels. A user can have one incoming invitation at a time. The recipient reviews the proposed primary and confirms or declines using the invitation ID. Confirmation checks the current relationship state of both accounts before creating the link. Stale or competing requests cannot replace a relationship.

Release removes the family link and revokes switched family sessions. Profile data, PINs and direct sign-ins are preserved. Before releasing an email-less child, switch to the child and add a sign-in address in Profile. If email confirmation is required, verify that address. The account response exposes readiness as `canRelease`. Apply the new Core migration before enabling invitations. Existing users start without a pending invitation. Downgrading discards outstanding invitations.

Permanent deletion of a primary with linked subaccounts returns 409 with `{"reason":"linkedSubaccounts"}` from `POST /users/{id}/hard/delete`. The links and all linked users are preserved.

## Earlier schema changes

Family responses return stored display names, which may be empty or whitespace for existing accounts. Create and update requests still require a nonblank display name. Version 1.2.1 aligns the schema with server validation for PIN clearing. Clients upgrading their schema should accept stored display names in responses and handle 409 when two updates race to set a first PIN. The upgrade from 1.2.0 requires no additional data migration.

## PIN management in 2.0.0

A primary user can set, change or remove their own switching PIN or a linked account’s PIN with `PUT /profile/subaccounts/{userId}/pin` (body: `pinMutation`, success: 204). Changing a PIN in the existing subaccount editor uses the same verification. Creation requires proof when either initial PIN is supplied. Profile-only edits and creation without a PIN do not require proof.

Send a `proof` object with one credential: the target’s `currentPin`, the signed-in primary user’s `password`, or a one-time `magicToken`. For creation the target is the primary user. Request an email link using `POST /profile/subaccounts/{userId}/pin-proof` with `operation` set to `change`, `remove` or `create` (204). Copy the code from the link into the original form. The code expires after the configured magic-link lifetime and can authorize only the requested actor, target and operation. Missing or invalid proof returns 403. Password and magic-link methods require their respective account modes.

Authorized administrators can remove a switching PIN with `POST /users/{id}/pin/unset` (204). The user list exposes `permissions.canUnsetPin` for eligible rows. The endpoint checks User.Manage and the existing moderation rules.

Clients upgrading from 1.2.1 must supply proof for PIN mutations. Stored PINs and family links need no migration.

## Primary PIN proof in 2.1.0

A signed-in primary user can also supply `proof.primaryPin` as the sole credential when setting, changing or removing the PIN of a linked subaccount. This applies to both `PUT /profile/subaccounts/{userId}` and `PUT /profile/subaccounts/{userId}/pin`. A primary user managing their own PIN still supplies it as `proof.currentPin`. Account creation continues to use the 2.0.0 proof options. The top-level `primaryPin` on the create request still sets the primary account's first PIN.

The new credential is optional. Existing clients can keep sending their current proof fields. Upgrading from 2.0.0 requires no data migration.

## PIN status for linked subaccounts

The `currentHasPin` boolean, introduced in 2.2.0, indicates whether the signed-in account has a switching PIN. A linked subaccount receives its own PIN status both after direct sign-in and after switching into it. Clients should hide the self-service controls when this field is absent.

## Managing a linked subaccount’s own PIN in 2.3.0

A signed-in, active linked subaccount can use `PUT /profile/subaccounts/{userId}/pin` with its own user ID and the `selfPinMutation` body. Send `{"pin":"0042"}` to set a PIN, omitting `proof` or passing null. The server accepts setup only while no PIN is stored, including after the previous PIN has been removed. To change an existing PIN, send `{"pin":"0042","proof":{"currentPin":"0017"}}`. To remove it, send `{"clearPin":true,"proof":{"currentPin":"0017"}}`. All three operations return 204 on success.

Requests targeting another user return 403. Changes and removals require the correct current PIN as the sole credential. If a PIN already exists, setup without proof returns 403. A concurrent write conflict returns 409 and preserves the PIN saved by the winning request. Fetch the family response again after a rejected setup to show the current controls. The configured PIN limit applies to setup as it does to replacement.

Requests from 2.2.0 clients continue to work on 2.3.0 servers. Enable first-time setup only for servers that support 2.3.0. The family response is unchanged and no data migration is required.
