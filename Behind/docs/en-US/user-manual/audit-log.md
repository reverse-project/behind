# Audit log

The **Audit log** page lists what changed in the application, when, and who did it. It is read-only: entries cannot be edited or removed.

You need the `AuditLog.Read` grant. Open it from the **Audit log** tile on the [Administration](./administration.md) page, or go to `#/audit-log`. Without the grant you see "You need audit log access to see the audit log."

## What you see

Each row shows:

- **When** - the time of the change, in your local time.
- **Who** - the person who made the change, their email addresses, and, when it was done for someone else, "On behalf of" that person. Changes made by the application itself show as **System**, and changes without a signed-in person as **Guest**.
- **Action** - `Created`, `Updated`, `Deleted`, `HardDeleted`, `Restored`, `ChangeOwnership`, `Moved`, `ImpersonationStarted`, or `ImpersonationEnded`.
- **Entity** - the translated kind of record and a readable name for it (for example a user's display name or a role name). Types without a translation fall back to their recorded name. Hover the name to see the raw key. Identifiers that no longer exist are shown as they are.
- **Changes** - for updates, each changed property with its old and new value. Sensitive values such as passwords are always shown as `***`.

## Finding entries

- **From** and **To** pick the dates to show. The default is the last seven days. The start date must not be after the end date, and the dates are UTC days.
- **Search** matches recorded entity type names, keys and resolved labels, actor and on-behalf-of names and emails, and changed property names and values, ignoring case.
- **Entity type**, **Action**, **Actor** and **On behalf of** narrow the results together. Choose an exact entity type from all types recorded in the audit log, or choose all types. Person filters search names and email addresses.
- **Sort by** selects time, entity type, entity key, action or actor. **Order** selects ascending or descending; the default is newest first.
- **Reset** returns to the defaults.

Advanced searches and non-time sorts examine the selected date range before paging; use a narrower range for large audit logs.

## Impersonation in the audit log

While an administrator [impersonates](./impersonation.md) someone, every change is recorded with the administrator in **Who** and the impersonated person in **On behalf of**. Starting and ending an impersonation are entries of their own (`ImpersonationStarted` and `ImpersonationEnded` on a `ValidSession`), so you can search for the administrator's name to see everything done during that time.

Entries are shown 20 to a page with the page buttons below the table.
