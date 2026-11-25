# Subaccount API 1.2.0

[JSON Schema](../../schemas/subaccounts-1.2.0.schema.json)

All routes require an authenticated session. IDs identify users. Request and response bodies use the named definitions in the schema.

| Method and path | Input | Success |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 with the usual sign-in tokens |
| GET /profile/subaccounts/candidates?query=…&page=1&pageSize=10 | — | 200 `candidateList` |
| POST /profile/subaccounts/{userId}/request | — | 204 |
| GET /profile/subaccounts/requests | — | 200 `invitationList` |
| POST /profile/subaccounts/requests/{requestId}/confirm | — | 204 |
| POST /profile/subaccounts/requests/{requestId}/decline | — | 204 |
| DELETE /profile/subaccounts/{userId} | — | 204 |

The primary account creates new users and edits their display name, birthday, child flag and PIN. Birthdays use YYYY-MM-DD and cannot be in the future. Omitting the PIN on update preserves it. Set `clearPin` to true without a PIN to remove it. A PIN is a string of digits, including any leading zeros.

The optional `primaryPin` field on create sets the primary account’s first switching PIN. Omit it to skip setup. The `pin` field belongs to the subaccount. Eligible family sessions can return to the active primary account using its PIN. Without one, full sign-in is required. Direct subaccount sign-ins cannot switch. The family response includes `primaryHasPin` (false for direct subaccount sign-ins). The primary appears in `accounts` only when eligible for PIN switching. Only the primary can see birthdays and email addresses. Successful switching invalidates the source session.

Invalid input returns 400, a forbidden switch or missing or incorrect required PIN returns 403, and an unavailable target returns 404. Duplicate email or attempting to replace an existing primary PIN during creation returns 409. Switch rate limiting returns 429. Version 1.0 clients may omit the new request field. Before deployment, apply the Core migration for SQLite or PostgreSQL. It copies existing hashes unchanged to their owners and leaves primary PIN setup optional.

Rolling back restores subaccount hashes to their links. The 1.0 schema cannot store primary PINs, so users must set those again after a rollback and subsequent upgrade.


## Linking existing users

Both security account flags must be enabled. Disabled routes return 404. The family response includes `linkUnlinkEnabled` for the current session. A pending invitation confers no family access and cannot be accepted while either flag is disabled. Re-enabling both allows the recipient to review an outstanding invitation again.

A directly signed-in primary may browse eligible users by display name. Candidates must be active, have an email and have neither a parent nor children. Candidate and invitation results expose public labels. A user can have one incoming invitation at a time. The recipient reviews the proposed primary and confirms or declines using the invitation ID. Confirmation checks the current relationship state of both accounts before creating the link. Stale or competing requests cannot replace a relationship.

Release removes the family link and revokes switched family sessions. Profile data, PINs and direct sign-ins are preserved. Before releasing an email-less child, switch to the child and add a sign-in address in Profile. If email confirmation is required, verify that address. The account response exposes readiness as `canRelease`. Apply the new Core migration before enabling invitations. Existing users start without a pending invitation. Downgrading discards outstanding invitations.
