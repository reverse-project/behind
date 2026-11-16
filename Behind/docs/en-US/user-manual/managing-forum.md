# Forum

The main web installation includes **Forum**. Boards can be assigned to a category; the public board list groups boards by category, with uncategorized boards shown separately. Choose a board, open a topic to read replies, or sign in to create a topic or reply. The board and topic lists, breadcrumb navigation and author panels provide a simplified, mobile-friendly forum layout. Authors appear by their tagged display name and profile picture, linked to their public profile. Blank names use `user#tag`, never an email address. Anonymous messages hide the author's profile and picture from ordinary readers, while authorized administrators and moderators can see the real author. Only unavailable accounts show **Unknown author**. Pending/rejected messages are not public. Removing a board or rejecting/deleting a topic also hides its replies.

## Writing messages

Use the **Visual editor** toolbar for bold, italic, underline, strikethrough, quotes, code, lists and links. Switch to **BBCode** to edit the source or **Preview** to see how the message will appear. Topics and replies store BBCode, with a limit of **20,000 characters including tags**. The moderation queue uses the same formatting.

Links in messages, previews and the moderation queue always ask for confirmation showing the destination URL. Confirmed links open in a new tab, never replacing the forum page. Cancel leaves the link unopened.

Supported tags: `[b]bold[/b]`, `[i]italic[/i]`, `[u]underline[/u]`, `[s]strikethrough[/s]`, `[quote]quote[/quote]`, `[code]code[/code]`, `[list][*]item[*]item[/list]`, `[list=1][*]numbered item[/list]`, `[url=https://example.com]link[/url]` and `[url]https://example.com[/url]`. Links accept HTTP, HTTPS and mailto addresses. `[lb]` and `[rb]` represent literal square brackets. Existing plain text remains readable; HTML is displayed as text, never executed.

## Moderation

**Administration > Manage forum** (`Forum.Manage`) manages board categories, boards and user moderation rules. Create categories in **Board categories**, then select one while creating or editing a board; choose **Uncategorized** to leave a board outside a category. Categories with assigned boards cannot be deleted until their boards are reassigned or removed. **Administration > Forum moderation** (`ForumModeration.Manage`) opens the same page with moderation controls. Choose **Pending**, **Published** or **Rejected**, then **Refresh**. **Publish** approves an awaiting message; **Reject** also withdraws an already published message.

On **Settings > forum.json** (`Configuration.Manage`), choose:

- `Forum:Moderation`: `Post` (default: publish immediately, moderate afterwards) or `Pre` (approval before publication).
- `Forum:AllowBoardOverrides`: enable an optional mode on each board; **Use global setting** inherits the global mode.
- `Forum:AllowUserOverrides`: enable a mode per user. Enter the user id from **Administration > Users** to create/update the rule; deleting the rule restores inheritance.

Enabled user overrides take precedence over enabled board overrides, then the global mode. Disabled overrides are ignored but retained. Settings reload without restarting and affect **new messages only**, not existing pending/published messages. Site-wide configuration editing remains a developer capability; forum managers can edit overrides only when enabled.

Delete soft-deletes records. Developers can select **Show deleted** and **Refresh**, restore records or permanently delete them. A category assigned to a board, or a board/topic with stored messages/replies, cannot be permanently deleted until its references or children are removed. Changes remain versioned and audit logged.

## Profile picture

Open **Profile**, choose a PNG, JPEG, GIF or WebP image up to **2 MiB**, and it is saved immediately. **Remove** clears your picture. Your picture and tagged display name appear beside non-anonymous forum messages and link to your public profile. Email is never a name fallback; it is shared on your profile only if you explicitly enable it. Previously downloaded pictures can remain in browser caches after removal.
