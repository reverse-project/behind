# Managing Users

Open **Administration > Users**. You need the `User.Manage` grant.

## Finding users

- **Search** by name or email and choose **Search**.
- Use the **letter bar** to filter by the first letter of the name (or email when there is no name). Letters with no users are disabled; **#** covers everything that is not a letter.
- Use the pager at the bottom to move between pages.

Each user shows their name, emails, role badges, and any ban message. Badges mark **You**, **Banned**, and **Deleted** users.

Buttons only appear or work when you are allowed to use them. You can never moderate yourself, and only developers can moderate other developers.

## Roles shortcuts

**Make administrator** / **Remove administrator** and **Make developer** / **Remove developer** add or remove those built-in roles. Only developers can change the developer role.

For any other role, use the [Access panel](#editing-a-users-access).

## Signing a user out

**Sign out everywhere** ends all sessions of that user.

**Impersonate** lets an administrator act as the user, see [Impersonation](./impersonation.md).

## Banning and unbanning

1. Choose **Ban**.
2. Enter the message to show on the [wall of shame](./wall-of-shame.md) (required, up to 500 characters).
3. Choose **Ban user**.

The user is banned and signed out of all sessions. **Unban** lifts the ban.

## Deleting and restoring

- **Delete** removes the user. It needs the `User.Delete` grant and you are asked to confirm. A deleted user can be restored by a developer.
- Developers also see **Show deleted users**. For deleted users they can **Restore** the account or **Delete permanently**. A permanent delete cannot be undone and is refused when the audit log still refers to the user.

## Editing a user's access

Choose **Access** on a user to open the access panel. Choose **Hide access** to close it. It has four parts.

### Calculated grants

The grants the user effectively has: all grants from their roles plus their direct grants, minus every restriction. It updates as you change the selections below, before you save. See [how it is calculated](./managing-roles-and-access.md#how-calculated-grants-work).

### Roles

Tick the roles the user should have. The **Users** role is required and cannot be unticked.

### Direct grants and restrictions

Each grant has a drop-down:

- **Not set**: no direct effect.
- **Grant**: the user gets this grant on top of their roles.
- **Restrict**: the user loses this grant even if a role gives it.

### Saving

Choose **Save access**. The list refreshes and shows "The access was saved." The user sees the change when their session next refreshes.

The panel is read-only when you cannot edit that user (yourself, or a developer when you are not one). The save is refused with a message when:

- you try to give the user a grant you do not hold yourself (through a role, a direct grant, or by lifting a restriction), or
- a selection is not valid.
