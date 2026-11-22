# Subaccount API 1.0.0

[JSON Schema](../../schemas/subaccounts-1.0.0.schema.json)

All routes require an authenticated session. IDs identify users. Request and response bodies use the named definitions in the schema.

| Method and path | Input | Success |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 with the usual sign-in tokens |

The primary account creates new users and edits their display name, birthday, child flag and PIN. Birthdays use YYYY-MM-DD and cannot be in the future. Omitting the PIN on update preserves it. Set `clearPin` to true without a PIN to remove it. A PIN is a string of digits, including any leading zeros.

A family-authorized session can switch to an eligible sibling. A direct subaccount sign-in has no sibling access. Email and birthday in the family list are visible only to the primary. Successful switching invalidates the source session. Returning to the primary requires its regular sign-in.

Invalid input returns 400, an unauthorized switch or incorrect PIN returns 403, and an unavailable target returns 404. Duplicate email returns 409 and switch rate limiting returns 429. Version 1.0.0 introduces these endpoints. Apply the Core database migration before deployment. Existing users and sessions require no conversion.
