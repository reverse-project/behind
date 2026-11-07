# Access settings

The **Access settings** page controls who can see the website and who can sign in. It has two independent tools:

- the **restriction page**: only users with a chosen role see the website contents, everybody else sees a page with a message you write,
- **sign-up moderation**: new accounts have to be approved before they can sign in.

You find the page under **Administration > Access settings** (`#/manage/access-settings`).

| Action | You need |
|--------|----------|
| See the settings and the users waiting for approval | the `Access.Read` grant |
| Change the settings, approve and reject users | the `Access.Write` grant (rejecting also needs the `User.Delete` grant) |
| Reset the settings to their defaults | the `Access.Delete` grant |
| See, restore and permanently delete reset settings | the `RawDatabase.Manage` grant (developers) |

Administrators have the first three grants by default. Users do not.

## The restriction page

1. Turn on **Show the restriction page to visitors without the required role**.
2. Choose the **Required role**. Choose **Users** to let every registered user in; choose another role, for example a role you gave only to your members, to keep the website for them.
3. Optionally type a **Title**, the heading of the restriction page. When it is empty the heading is **Restricted website**.
4. Write the **Message** visitors see in the rich text editor (headings, bold, lists, links, images and so on). Use the image button or drop an image into the editor to upload it; images are stored with the access settings and are visible to restricted visitors too. When it is empty a default message is shown.
5. Press **Save**.

While the restriction is on:

- visitors without the role (including visitors who are not signed in) see only the restriction page with your message and **Sign in** and **Sign up** buttons. The content of the website, for example sites, news, articles, galleries, downloads and menus, is not sent to them at all,
- the sign-in, sign-up, magic link and email confirmation pages keep working, so people can get in,
- users with the **Access settings management** grant (`Access.Manage`, administrators by default) always see the website, so the restriction can never lock the administrators out,
- a signed-in user without the role sees the restriction page and the note that the account is not allowed to see the website.

## Sign-up moderation

Turn on **Only approved users can sign in** and press **Save**.

- A new account is created as usual but waits for approval. When its owner signs in (with a password or a magic link) the application answers that the account is waiting for approval. The sign-up page tells the same.
- The waiting accounts are listed under **Users waiting for approval**, with a **Search** box. **Approve** lets the user sign in from now on. **Reject** deletes the account (it can be restored by a developer on the [Users](./managing-users.md) page).
- Existing users are not affected, and turning the setting off later does not approve the waiting accounts: they stay in the list until you approve or reject them.

## Resetting and restoring

- **Reset to defaults** soft deletes the saved settings: the restriction page and the moderation are switched off. Accounts that already wait for approval keep waiting.
- Developers see a **Show deleted access settings** switch. Deleted settings show **Restore** and **Delete permanently**.

Every save is a new version of the settings and every change, approval and rejection is recorded in the [audit log](./audit-log.md).
