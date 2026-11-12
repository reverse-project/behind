# Managing sites

## Page layouts and widgets

Open **Administration > Layouts** (`#/manage/layouts`). Viewing requires `Configuration.Read`; saving requires `Configuration.Manage` and configuration must not be read-only. Add or copy a layout, choose the default, then arrange sections and widgets using rows, columns and spans. The preview shows placement; Save applies all edits together. Each layout needs exactly one **Site content** widget, which displays the currently routed page. The initial layout contains only that widget and preserves the existing appearance.

Add latest news, articles or downloads, horizontal/vertical menus (by menu name), custom rich text or HTML, chat, or installed module widgets such as Forum posts/statistics. Unsafe HTML and scripts are removed; HTML editing is not a way to run JavaScript. Widget data respects the original content access rules. Small screens stack the widgets in their configured order.

Widgets use Bootstrap cards by default, with list and statistics styling where applicable. Site themes can customize their colors; the site content itself keeps its existing appearance.

Assign layouts to exact hash routes (`#/news`) or prefixes ending with `*` (`#/site/*`); the most specific match wins. Unassigned routes use the default. Account and administration pages always keep the simple layout so an incorrect layout cannot hide the editor. Choose another default before deleting the default layout. Deletion is soft; developers can show deleted layouts, restore or permanently delete them. Save after each lifecycle action to persist it. A conflict means another editor saved first: reload before retrying.

Menu changes refresh mounted menus automatically without losing page content. Layout configuration is backed up as `layouts.json` in the configured data directory, separately from the database.

### Chat

Chat history is kept in the database without the former 200-message limit. Messages appear oldest first; scroll up to load earlier messages automatically, or use **Load older messages**. Messages show the author's profile picture (or initial), creation time and last edit time. Sign in to send up to 1,000 characters or **Reply** to an existing message. **Edit** is available only on your own messages for five minutes after creation; editing does not restart this period. Concurrent edits are rejected instead of overwriting newer text.

Messages appear immediately. Guests may read; signed-in users receive **Chat.Read** and **Chat.Write** by default. In role or user access management, restrict **Chat.Write** to stop posting, replying and editing, or restrict **Chat.Read** to hide the chat and block its API for that signed-in user. Restrictions override role grants. Because guest reading is enabled, a restricted user can still read public chat after signing out.

**Chat.Delete** allows removal of one's own messages; grant **Chat.Manage** to moderators so they can **Remove** any message. Neither overrides the author-only five-minute edit window. Removal is audited and hides every version, including quoted reply previews. The old `widget-chat.json` file stays untouched and is not imported; the database chat starts fresh.

**Sites** are content pages that administrators write in the browser and everyone can read. You find them under **Administration > Sites** (`#/sites`).

| Action | You need |
|--------|----------|
| See the list of sites and their versions | the `Site.Read` grant |
| Create sites, save versions, publish, make a version live and upload images | the `Site.Write` grant |

Administrators have both grants by default.

## Finding sites

The list has a **Search** box (matches the title and the slug of the newest version), a letter filter on the title and page buttons, like the users page. Sites are listed alphabetically by title.

## Deleting and restoring

- **Delete** (the `Site.Delete` grant) soft deletes the whole site with all its versions. It stops being served, its slug is free again, and nothing is lost.
- Developers (the `RawDatabase.Manage` grant) see a **Show deleted sites** switch. A deleted site shows **Restore**, which brings back every version and the version that was live, and **Delete permanently**, which removes the site, its versions and its live version for good. Restoring is refused when another site has taken the slug in the meantime.

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

## Sections

Sections turn a site into a landing page style layout. They are shown below the content, in the order you set, and are part of the version you save: **Save draft** and **Save and publish** store the sections together with the title and the content, older versions keep their own sections, and **Load into editor** brings the sections of that version back.

- **Add section** appends a section (a site can have up to 30). Use the arrow buttons to move a section earlier or later and **Remove** to take it out; nothing changes on the page until you save.
- **Heading** is optional.
- **Theme** is **Normal** (plain, like the rest of the page) or **Accented** (a block in the accent colour of the site, with light text).
- **Text** is written in the same rich text editor as the content.
- **Picture** is optional (PNG, JPEG, GIF or WebP, up to the image limit). With a picture you can add a description (shown when the picture cannot be seen and read by screen readers) and choose whether the picture is on the **left** or the **right** of the text. On narrow screens the picture is shown above the text. **Remove picture** takes it out of the section.
- Every section needs a heading, a text or a picture, otherwise the site cannot be saved.

A site with sections can have an empty content, then the page only shows the sections (and the carousel, when there is one). The text of the sections is cleaned like the content.

## Image carousel

A site can have an image carousel: a slider shown above the content of the live page that moves on by itself. Using it is your choice for every site: a site without images is shown as before. Open an existing site in the editor and use the **Image carousel** section (it is not shown for a new site until it is saved).

- **Add images** uploads one or more pictures (PNG, JPEG, GIF or WebP, up to the image size limit, with a limited number of images; the limits are shown in the section). Use the arrows to change the order and the trash button to remove a picture from the carousel.
- Every picture has an optional **caption**, shown over the picture, and an optional **link** that opens when the picture is clicked. A link starts with `https://`, `http://`, `mailto:`, `#/` (a page of this site) or `/`.
- **Seconds per image** sets how long a picture stays (within the allowed range shown below the field). The **Preview** shows the slider as visitors see it.
- **Save carousel** stores a new version of the carousel and shows it on the page right away, as long as the site itself is live. The carousel of a draft site is not shown to visitors.
- Visitors can use the previous and next buttons, the dots and swipe gestures. The slider pauses while the pointer is over it, and it does not move by itself for visitors whose device asks for reduced motion.
- **Remove carousel** (needs `Site.Delete`) hides it. A developer sees a **Deleted** badge in the section and can **Restore** it or **Delete permanently** (this also deletes its images). The carousel follows its site: deleting, restoring or permanently deleting a site does the same with its carousel.

## Addresses

A live site is served at `#/<slug>`. If the slug is the same as a built-in page (for example `users`), the built-in page wins, and the site stays reachable at the fallback address `#/site/<slug>`. The editor lists both addresses of the live version.

### Custom index

The site administrator can name one slug as the **custom index** in `frontend.json` (`Frontend:Sites:IndexSlug`). While a site with that slug is live, the start page `#/` shows that site instead of the built-in welcome page. The site is still served at its own address too. The editor shows a **Custom index** badge on the live version and on the Sites list, and tells you when the slug you typed is the custom index. Without the setting, or while no site with that slug is live, `#/` shows the welcome page.

An address with no live site shows "This page does not exist."

Everything you change here is recorded in the [audit log](./audit-log.md) with the title of the site instead of its identifier.
