# Raw database

The **Raw database** editor lets developers look at and change the rows of the database exactly as they are stored. It skips the checks and the soft delete of the management pages, so use it only when a management page cannot do what you need.

You find it under **Administration > Raw database** (`#/manage/raw-database`). It needs the `RawDatabase.Manage` grant, which only developers hold.

## Tables

Choose a table in the **Table** list. The list shows every table of the database with its number of rows: the tables of all entities (users, roles, sites, news, articles, galleries, downloads, menus, files, sessions, ...), their join tables (for example the roles of a user) and the version tables. A new entity shows up here automatically.

The **AuditLogEntries** table is read-only: you can look at it but not change it.

## Looking at rows

- Rows are shown with every column and are paged.
- **Search** looks for the text in every column that holds text, numbers, ids or dates (binary and sensitive columns are not searched).
- Click a column name to sort by it, click it again to reverse the order.
- A value shown as *NULL* is empty in the database; an empty text is a different thing.
- Sensitive values, such as password hashes and tokens, are always shown as `***`.

## Changing rows

| Action | What happens |
|--------|--------------|
| **New row** | Fill in the columns and save. Empty fields are left out; a key column of type guid left empty gets a new generated id. Tick **NULL** to store an empty value. |
| **Edit** | Change the columns and save. Only the columns you changed are written. Key columns cannot be changed. A `***` value stays as stored until you type a new one. |
| **Delete** | Removes the row permanently after you confirm. There is no restore. |

If the database refuses a change, for example because a value must be unique or a related row is missing, the page shows the message of the database and nothing is written. If a value does not fit its column, such as text in a number column, the page tells which column it is.

## Audit log

Every raw change is recorded in the [Audit log](./audit-log.md) like the changes of the management pages: **Created** for a new row, **Updated** with the old and the new value of every changed column (sensitive values are masked) and **Deleted permanently** for a deleted row. The change and its audit entry are saved together or not at all.
