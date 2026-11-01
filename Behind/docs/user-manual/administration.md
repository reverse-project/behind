# Administration

**Administration** is a page of shortcuts to the management tools you may use. It appears in your name menu only when your account may use at least one of them.

| Tool | You need | Manual |
|------|----------|--------|
| Users | the `User.Manage` grant | [Managing users](./managing-users.md) |
| Roles | the `Role.Manage` grant | [Managing roles and access](./managing-roles-and-access.md) |
| Sites | the `Site.Read` grant (`Site.Write` to change them) | [Managing sites](./managing-sites.md) |
| Audit log | the `AuditLog.Read` grant | [Audit log](./audit-log.md) |

Administrators have all of these grants by default. Impersonating a user needs the `Impersonation.Manage` grant, see [Impersonation](./impersonation.md). If you open an administration page without the needed access you see a message such as "You need administrator access to manage users."

## Who can do what

The application separates three kinds of people:

- **Users** can read the basic areas and manage their own account.
- **Administrators** manage users and roles.
- **Developers** can additionally see deleted users and use the raw database and configuration areas. A developer holds the `RawDatabase.Manage` grant.

Administrators can never go beyond their own access. They cannot give a role or user a grant they do not hold themselves, cannot change other developers, and cannot change their own access. Only developers are exempt from the "cannot give what you do not hold" rule.
