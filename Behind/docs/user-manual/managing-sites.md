# Managing sites

**Sites** are content pages that administrators write in the browser and everyone can read. You find them under **Administration > Sites** (`#/sites`).

| Action | You need |
|--------|----------|
| See the list of sites and their versions | the `Site.Read` grant |
| Create sites, save versions, publish, make a version live and upload images | the `Site.Write` grant |

Administrators have both grants by default.

## Versions, drafts and the live version

Every save creates a **new version** of the site, nothing is ever overwritten.

- **Save draft** stores a new version that is not published. A draft is never shown to visitors.
- **Save and publish** stores a new version, publishes it and makes it the **live** version.
- Only the **live** version is shown to visitors. Saving a draft of a live site does not change what visitors see.
- **Load into editor** copies an older version into the editor. Saving it creates a new version based on it.
- **Make live** switches the live version to any version of the site, which is also how you roll back. A draft that is made live is published at that moment.

Each version row shows its status (Draft, Published or Live), its title, its slug, when it was saved and when it was published.

## Title, slug and content

- The **title** is shown above the content.
- The **slug** is the address of the site. It is generated from the title (lowercase letters, numbers and dashes) until you type in the slug field yourself. **Generate from title** resets it. Every version has its own slug, and two sites cannot use the same slug at the same time: a slug is taken while it belongs to the live or the newest version of another site.
- The **content** is written in a rich text editor with headings, bold, italic, underline, strike-through, lists, indentation, alignment, quotes, code blocks and links. Use the image button to upload a picture (PNG, JPEG, GIF or WebP, up to the limit shown under the editor, 5 MB by default). Dropping or pasting an image file into the editor uploads it as well.

The content is cleaned when it is saved: scripts, styles, event handlers and unknown tags are removed.

## Addresses

A live site is served at `#/<slug>`. If the slug is the same as a built-in page (for example `users`), the built-in page wins, and the site stays reachable at the fallback address `#/site/<slug>`. The editor lists both addresses of the live version.

### Custom index

The site administrator can name one slug as the **custom index** in `frontend.json` (`Frontend:Sites:IndexSlug`). While a site with that slug is live, the start page `#/` shows that site instead of the built-in welcome page. The site is still served at its own address too. The editor shows a **Custom index** badge on the live version and on the Sites list, and tells you when the slug you typed is the custom index. Without the setting, or while no site with that slug is live, `#/` shows the welcome page.

An address with no live site shows "This page does not exist."

Everything you change here is recorded in the [audit log](./audit-log.md) with the title of the site instead of its identifier.
