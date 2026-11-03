# Menus

**Menus** are the lists of links the application shows. The **main menu** is built in: it holds the tabs of the top bar (**Index**, **News**, **Articles**, **Galleries**, **Wall of shame**) and the entries of your name menu (**Profile**, **Session**, **Sessions** and **Administration**). You can add more menus, for example a **footer** menu, which is shown at the bottom of every page next to the version and the licenses.

You find the tools under **Administration > Menus** (`#/manage/menus`).

| Action | You need |
|--------|----------|
| See the menus and their items | the `Menu.Read` grant |
| Create and edit menus and items | the `Menu.Write` grant |
| Delete menus and items | the `Menu.Delete` grant |
| See, restore and permanently delete deleted menus and items | the `RawDatabase.Manage` grant (developers) |

Administrators have the first three grants by default. Users do not.

## Menus

A menu has a **name** and a **title**.

- The **name** is how the menu is requested, for example `footer`. It is turned into lowercase letters, digits and dashes and must be unique, ignoring upper and lower case. The page shows the menu named `main` as its top bar and the menu named `footer` at the bottom.
- The **title** is the readable name shown in the lists.
- The built-in **main** menu has a *Built-in* badge. Its name cannot be changed and it cannot be deleted, but you can change its title and everything inside it.

**Items** opens the items of a menu.

The lists of menus and of menu items have a **Search** box, **letter buttons** that show only the entries whose title starts with that letter, and page buttons at the bottom. The items list is flat, ordered by position and then title, and a sub item shows which item is its parent.

## Menu items

An item has:

- a **title**, the text of the link,
- a **URL**: a page of this application (`#/news`), another address of this site (`/about`) or an outside address starting with `http://` or `https://`, a `mailto:` or a `tel:` link. Other kinds of addresses (for example `javascript:`) are refused,
- **Open in**: **Current page**, **New tab** or **New window** (a small separate browser window),
- a **position**: items are ordered by position, then by title,
- an optional **parent**. An item with a parent is a **sub item**: its parent turns into a drop down menu that lists its sub items. Only top level links of the same menu can be parents and sub items are one level deep. An item that has sub items cannot become a sub item itself,
- the **roles** that see it.

### Who sees an item

Leave every role unchecked and the item is shown to **everybody**, including visitors who are not signed in. Check one or more roles and the item is shown only to the signed in users who have **at least one of them**. This is how the **Administration** entry is hidden from everybody but administrators and developers. A sub item is also hidden when its parent is hidden. Hiding an item only hides the link: the page behind it still checks the access of the user.

If a role is deleted, the items that were limited to it are shown to nobody until the role is restored.

### User menu items

In the main menu an item can be a **user menu item**: it is listed in the menu of the signed in user (under the user name) and not in the top bar. User menu items can only be added to the main menu, at the top level, so the **Kind** field is only offered there. **Sign out** is always the last entry of the user menu and cannot be changed.

## Deleting and restoring

- **Delete** soft deletes a menu or an item. A deleted menu is hidden together with its items, a deleted item is hidden together with its sub items. Nothing is lost.
- Developers see a **Show deleted** switch on both lists. A deleted entry shows **Restore** and **Delete permanently**. A menu can only be restored while no other menu uses its name.
- **Delete permanently** of a menu removes all of its items, of an item removes its sub items.

Everything you change here is recorded in the [audit log](./audit-log.md) with the title of the menu or the item instead of identifiers.
