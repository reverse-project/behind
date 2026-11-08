# Route aliases

A **route alias** gives a page a short route. If a download directory lives at `#/downloads/products/manuals`, an alias called `manuals` lets visitors open it with `#/manuals` instead. The short route opens the target page and stays in the address bar of the browser.

You find the tool under **Administration > Route aliases** (`#/manage/route-aliases`).

| Action | You need |
|--------|----------|
| See the route aliases | the `RouteAlias.Read` grant |
| Create and edit route aliases | the `RouteAlias.Write` grant |
| Delete route aliases | the `RouteAlias.Delete` grant |
| See, restore and permanently delete deleted route aliases | the `RawDatabase.Manage` grant (developers) |

Administrators have the first three grants by default. Users do not. Opening a short route needs no grant: the page behind it checks the access of the visitor as usual.

## Route aliases

A route alias has two fields:

- the **short route**, typed after `#/`, for example `manuals`. It is turned into lowercase letters, digits and dashes and must be unique among the route aliases, ignoring upper and lower case,
- the **target route**, the page the short route opens. It must be a page of this application and start with `#/`, for example `#/downloads/products/manuals`. Spaces and other kinds of addresses (for example `https://` or `javascript:`) are refused.

To find the target route of a download directory, open it in **Downloads** and copy the address after the `#`.

The list has a **Search** box (it looks at the short route and the target), **letter buttons** for the first letter of the short route and page buttons. The short route of every entry is a link you can click to try it.

## Which page wins

- The built-in pages (`#/news`, `#/downloads`, `#/administration` and so on) always win. An alias with the same name as a built-in page is never opened.
- A route alias wins over a [site](./managing-sites.md) with the same address (`#/<slug>`).
- When no route alias matches, the address is shown as a site, as before.
- A route alias opens its target once. If the target is another short route, it is shown as a site and not followed again.

## Deleting and restoring

- **Delete** soft deletes a route alias. The short route stops working and can be used by another alias. Nothing is lost.
- Developers see a **Show deleted** switch. A deleted entry shows **Restore** and **Delete permanently**. A route alias can only be restored while no other one uses its short route.

Everything you change here is recorded in the [audit log](./audit-log.md) with the short route instead of identifiers.
