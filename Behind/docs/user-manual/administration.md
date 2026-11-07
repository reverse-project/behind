# Administration

**Administration** is a page of shortcuts to the management tools you may use. Its entry in your name menu is an item of the main menu that only the `Administrators` and `Developers` roles see by default; who sees it can be changed in [Menus](./managing-menus.md).

| Tool | You need | Manual |
|------|----------|--------|
| Users | the `User.Manage` grant | [Managing users](./managing-users.md) |
| Roles | the `Role.Manage` grant | [Managing roles and access](./managing-roles-and-access.md) |
| Sites | the `Site.Read` grant (`Site.Write` to change them) | [Managing sites](./managing-sites.md) |
| News and news categories | the `News.Read` grant (`News.Write` to change them) | [News](./managing-news.md) |
| Articles and article categories | the `Article.Read` grant (`Article.Write` to change them) | [Articles](./managing-articles.md) |
| Galleries | the `Gallery.Read` grant (`Gallery.Write` to change them) | [Galleries](./managing-galleries.md) |
| Downloads | the `Download.Read` grant (`Download.Write` to change them) | [Downloads](./managing-downloads.md) |
| Menus | the `Menu.Read` grant (`Menu.Write` to change them) | [Menus](./managing-menus.md) |
| Route aliases | the `RouteAlias.Read` grant (`RouteAlias.Write` to change them) | [Route aliases](./managing-route-aliases.md) |
| Access settings | the `Access.Read` grant (`Access.Write` to change them) | [Access settings](./managing-access-settings.md) |
| Raw database | the `RawDatabase.Manage` grant (developers) | [Raw database](./managing-raw-database.md) |
| Audit log | the `AuditLog.Read` grant | [Audit log](./audit-log.md) |
| Settings | the `Configuration.Read` grant (`Configuration.Manage` to change them) | [Settings](./managing-settings.md) |

Administrators have all of these grants by default. Impersonating a user needs the `Impersonation.Manage` grant, see [Impersonation](./impersonation.md). If you open an administration page without the needed access you see a message such as "You need administrator access to manage users."

## Who can do what

The application separates three kinds of people:

- **Users** can read the basic areas and manage their own account.
- **Administrators** manage users and roles.
- **Developers** can additionally see deleted users and use the raw database and configuration areas. A developer holds the `RawDatabase.Manage` grant.

Administrators can never go beyond their own access. They cannot give a role or user a grant they do not hold themselves, cannot change other developers, and cannot change their own access. Only developers are exempt from the "cannot give what you do not hold" rule.
