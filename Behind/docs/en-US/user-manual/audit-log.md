# Audit log

The **Audit log** page lists what changed in the application, when, and who did it. It is read-only: entries cannot be edited or removed.

You need the `AuditLog.Read` grant. Open it from the **Audit log** tile on the [Administration](./administration.md) page, or go to `#/audit-log`. Without the grant you see "You need audit log access to see the audit log."

## What you see

Each row shows:

- **When** - the time of the change, in your local time.
- **Who** - the person who made the change, their email addresses, and, when it was done for someone else, "On behalf of" that person. Changes made by the application itself show as **System**, and changes without a signed-in person as **Guest**.
- **Action** - `Created`, `Updated`, `Deleted`, `HardDeleted`, `Restored`, `ChangeOwnership`, `Moved`, `ImpersonationStarted`, or `ImpersonationEnded`.
- **Entity** - the kind of record and a readable name for it (for example a user's display name or a role name). Hover the name to see the raw key. Identifiers that no longer exist are shown as they are.
- **Changes** - for updates, each changed property with its old and new value. Sensitive values such as passwords are always shown as `***`.

## Finding entries

- **From** and **To** pick the dates to show. The default is the last seven days. The start date must not be after the end date, and the dates are UTC days.
- **Search** keeps only entries whose entity name or key contains the text, ignoring case.
- **Order** switches between newest first (default) and oldest first.
- **Reset** returns to the defaults.

## Impersonation in the audit log

While an administrator [impersonates](./impersonation.md) someone, every change is recorded with the administrator in **Who** and the impersonated person in **On behalf of**. Starting and ending an impersonation are entries of their own (`ImpersonationStarted` and `ImpersonationEnded` on a `ValidSession`), so you can search for the administrator's name to see everything done during that time.

Entries are shown 20 to a page with the page buttons below the table.
