# Behind User Manual

This manual describes what you can do in the Behind web application today.

## For everyone

- **[Getting started](./getting-started.md)** - creating an account, signing in, signing out, and the navigation bar.
- **[Your account](./your-account.md)** - profile, display name, current session, and all your sessions.
- **[Wall of shame](./wall-of-shame.md)** - the public list of banned accounts.

## For administrators and developers

- **[Administration](./administration.md)** - the Administration page and who can see it.
- **[Managing users](./managing-users.md)** - searching users, banning, deleting, restoring, signing users out, and editing a user's access.
- **[Managing roles and access](./managing-roles-and-access.md)** - roles, grants, restrictions, and how a user's calculated grants are worked out.
- **[Managing sites](./managing-sites.md)** - writing pages with the rich text editor, versions, drafts, publishing and addresses.
- **[News](./managing-news.md)** - reading and commenting on the news, writing news and managing the news categories.
- **[Articles](./managing-articles.md)** - reading and searching the articles, writing articles and managing the article categories.
- **[Galleries](./managing-galleries.md)** - browsing the galleries, uploading images in batches, describing them and publishing a gallery.
- **[Downloads](./managing-downloads.md)** - browsing the folder tree, downloading files, creating folders and uploading several files at once.
- **[Menus](./managing-menus.md)** - the main menu and other menus, their items, sub items, targets and who sees them.
- **[Quotes](./managing-quotes.md)** - the example module: reading the quotes and managing them with soft delete, restore and permanent delete.
- **[Route aliases](./managing-route-aliases.md)** - short routes for pages, for example `#/manuals` for a download directory.
- **[Access settings](./managing-access-settings.md)** - a restriction page that shows the website only to users with a role, and approval of new accounts before they can sign in.
- **[Impersonation](./impersonation.md)** - acting as another user to see what they see, and how it shows in the audit log.
- **[Raw database](./managing-raw-database.md)** - viewing, inserting, editing and permanently deleting the rows of any table (developers).
- **[Audit log](./audit-log.md)** - browsing who changed what and when.
- **[Settings](./managing-settings.md)** - the configuration files as tabs, masked secrets, generating and rotating secrets (developers).
- **[Elevating your own role](./elevation.md)** - becoming an administrator or developer with an authenticator code.

## Good to know

- The app needs JavaScript enabled.
- Pages that need you to be signed in show a "You are not signed in" message with a link to sign in.
- Pages for administrators show a message instead of data when your account lacks the access they need.
