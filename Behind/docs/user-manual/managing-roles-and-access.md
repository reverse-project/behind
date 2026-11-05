# Managing Roles and Access

Open **Administration > Roles**. You need the `Role.Manage` grant.

## Concepts

- A **grant** allows something. Grant names are `Area.Action`, for example `User.Manage`.
  - Areas: Session, AuditLog, Role, User, Configuration, Impersonation, RawDatabase.
  - Actions: Read, Write, Delete, Manage (Impersonation and RawDatabase only have Manage).
- A **restriction** removes a grant. It uses the same names as grants.
- A **role** is a named set of grants and restrictions. Users can have several roles.
- A user can also have **direct** grants and restrictions that apply to them alone.

### Built-in roles

| Role | Default grants |
|------|----------------|
| Users | Read on Session, Role, and User |
| Administrators | every action on Session, AuditLog, Role, and User, plus Impersonation.Manage |
| Developers | only the developer grants: Configuration.Read, Configuration.Manage and RawDatabase.Manage (every user also has the Users role) |

Built-in roles can be edited but not deleted. Changes you make to them are kept when the application restarts, so a database created before `AuditLog.Read` was removed from the Users role keeps it until an administrator removes it there.

### Developer grants and developer roles

`Configuration.*` and `RawDatabase.Manage` are **developer grants**. They can only belong to the built-in Developers role or to a custom role flagged as a **developer role**. Administrators can never obtain developer grants or developer roles, and they cannot pass them on, assign them to a user, or edit a developer role. Only developers can create a developer role, or turn the flag on or off for a custom role (choose **Make developer role** / **Remove developer flag**). A role cannot lose the flag while it still grants developer access, and developer grants cannot be added to a role that is not flagged. Developers can still give developer grants straight to a user.

### How calculated grants work

For a user:

1. Collect every grant from all of their roles and every direct grant.
2. Collect every restriction from all of their roles and every direct restriction.
3. Remove the restricted grants from the collected grants.

The result is the user's **calculated grants**. A restriction always wins over a grant, wherever the grant comes from. A restriction on one role therefore also affects grants the user gets from other roles.

Example: a user has the Administrators role and the direct restriction `User.Delete`. They keep every administrator grant except `User.Delete`.

This is how you give someone smaller access than their role: keep the role and add restrictions, or put restrictions on a role to reduce everyone who has it.

## The roles page

Each role shows its **Grants** (green) and **Restrictions** (red).

### Creating a role

Type a name (up to 100 characters) in **New role name** and choose **Create role**. Names must be unique, ignoring case. A new role has no grants.

### Editing a role

1. Choose **Edit access** on the role.
2. For each grant pick **Not set**, **Grant**, or **Restrict**.
3. Choose **Save**, or **Cancel** to discard.

A grant can be either granted or restricted on one role, not both. Restrictions on a role apply to every user who has the role.

### Resetting the grants of a built-in role

Choose **Reset grants** on a built-in role and confirm to set its grants back to the built-in defaults. Restrictions on the role are kept. The same rules as for editing a role apply, so you can only reset a role when you may change it and hold every grant it would gain.

### Deleting a role

Choose **Delete** on a custom role and confirm. This needs the `Role.Delete` grant. Users lose the access the role gave them. Built-in roles cannot be deleted. A deleted role is kept and can be restored by a developer.

### Searching and paging

The list has a **Search** box (matches the role name), a letter filter and page buttons, like the users page.

### Deleted roles (developers)

Developers see a **Show deleted roles** switch. Deleted roles show a **Deleted** badge with **Restore**, which brings the role back, and **Delete permanently**, which removes it for good. A role that is still referenced cannot be deleted permanently.

## Limits

- You can only edit a role whose grants you hold yourself, and only add grants you hold. Lifting a restriction counts as adding the grant.
- Developers can change any role and add any grant, but developer grants only fit developer roles.
- Nobody can add grants to a role they hold themselves (including by lifting a restriction); removing grants is allowed. You also cannot change your own access on the Users page.
- Administrators cannot touch developer grants or developer roles.
- Buttons that you may not use are hidden. If a save is refused you see a message and nothing is changed.

To give a role to a person, or to change one person's access, use the [Access panel on the Users page](./managing-users.md#editing-a-users-access).
