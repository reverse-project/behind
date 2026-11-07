# Quotes

**Quotes** is the example module of Behind: a list of short quotes with their author. It shows how a module adds its own page, administration tool, grants, menu item and database table to the application.

Everybody can read the quotes under **Quotes** in the main menu (`#/quotes`). You manage them under **Administration > Quotes** (`#/manage/quotes`).

| Action | You need |
|--------|----------|
| See the quotes in the management page | the `Quotes.Read` grant |
| Create and edit quotes | the `Quotes.Write` grant |
| Delete quotes | the `Quotes.Delete` grant |
| See, restore and permanently delete deleted quotes | the `RawDatabase.Manage` grant (developers) |

Administrators have all the grants by default, users can read.

## Writing quotes

A quote has a **quote** text (up to 1000 characters) and an **author** (up to 200 characters), both are required. **Edit** loads a quote into the form, **Save** stores it as a new version. A fresh installation starts with one welcome quote.

## Finding quotes

The search box looks in the text and in the author, the letter bar filters by the first letter of the author, and the list is paged.

## Deleting quotes

**Delete** hides a quote (it is only soft deleted). Developers switch on **Show deleted quotes** to see the deleted ones, then **Restore** a quote or **Delete permanently** to remove it for good. Every change appears in the [audit log](./audit-log.md) and the `Quotes` table can be edited in the [raw database](./managing-raw-database.md).
