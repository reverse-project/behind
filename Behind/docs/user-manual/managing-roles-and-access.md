# Managing Roles and Access

Open **Administration > Roles**. You need the `Role.Manage` grant.

## Concepts

- A **grant** allows something. Grant names are `Area.Action`, for example `User.Manage`.
  - Areas: Session, AuditLog, Role, User, Configuration, RawDatabase.
  - Actions: Read, Write, Delete, Manage (RawDatabase only has Manage).
- A **restriction** removes a grant. It uses the same names as grants.
- A **role** is a named set of grants and restrictions. Users can have several roles.
- A user can also have **direct** grants and restrictions that apply to them alone.

### Built-in roles

| Role | Default grants |
|------|----------------|
| Users | Read on Session, AuditLog, Role, and User |
| Administrators | every action on Session, AuditLog, Role, and User |
| Developers | the Users grants plus Configuration.Read and RawDatabase.Manage |

Built-in roles can be edited but not deleted. Changes you make to them are kept when the application restarts.

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

### Deleting a role

Choose **Delete** on a custom role and confirm. This needs the `Role.Delete` grant. Users lose the access the role gave them. Built-in roles cannot be deleted.

## Limits

- You can only edit a role whose grants you hold yourself, and only add grants you hold. Lifting a restriction counts as adding the grant.
- Developers can change any role and add any grant.
- Buttons that you may not use are hidden. If a save is refused you see a message and nothing is changed.

To give a role to a person, or to change one person's access, use the [Access panel on the Users page](./managing-users.md#editing-a-users-access).
