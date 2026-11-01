# Impersonation

**Impersonation** lets an administrator use the application as another user, for example to reproduce a problem that user reports. Everything done while impersonating is recorded in the [audit log](./audit-log.md).

You need the `Impersonation.Manage` grant. Administrators have it by default; users and developers do not.

## Starting

1. Open **Administration > Users**.
2. Choose **Impersonate** on the user and confirm.

You are taken to the start page and a banner shows "You are impersonating" with the user's name. You see the application exactly as that user does, with their access.

**Impersonate** only appears for users you may impersonate. You cannot impersonate:

- yourself, or anyone while you are already impersonating,
- banned or deleted users,
- developers, unless you are a developer,
- users holding a grant that you do not hold yourself. In that case you see "You are not allowed to impersonate this user."

## Stopping

Choose **Stop impersonating** in the banner. You are back in your own session. An impersonation also ends by itself after one hour, when the impersonated user is signed out of all sessions, or when you sign out.

## In the audit log

- Starting shows as `ImpersonationStarted` and stopping as `ImpersonationEnded`.
- Every change made in between shows you under **Who** and the impersonated user under **On behalf of**, never the impersonated user alone.
- The impersonated user sees the impersonation as an extra entry on their **Sessions** page.
