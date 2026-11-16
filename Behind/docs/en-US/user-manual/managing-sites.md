# Managing sites

## Optional content locales

Use the **Content locale** field in the existing content editor to assign a language or leave it unset. The choices are the application's available interface languages. A language assignment controls visibility, not automatic translation: create separate content for each translation. Content without a locale is shared across all selected locales, so you only need to localize content that differs. When no content locale is selected, only unlocalized content is shown.

Routes do not contain a locale. The selected locale is sent with content requests; its default comes from the signed-in user's profile, browser cache or site default. Sites and route aliases can reuse a slug in different locales, but two active entries cannot use the same slug in the same locale. Choose the locale when creating a translated site. Existing management pages remain the place to edit every translation.

Add a **Locale switch** widget in the layout editor, or enable the switch in a **Page header** widget. The switch changes the locale used for content requests without changing the route. For signed-in users, selecting a locale also saves it as the profile's interface language.

The RPDotHu and FSE headers respect the switch's enabled state and its selected presentation (view, flag-only, flag-and-text or text-only). Full-width header and footer sections retain the same site branding and edge-to-edge appearance as the fixed management shell, without the content panels' extra border or padding.

Owned content, such as site sections, gallery items and carousel slides, inherits its parent's locale when no own assignment is set. Assign a locale to a layout only when its header, navigation or widgets need to differ; layouts without a locale are shared. Menus without a locale are shared with localized pages too.

## Page layouts and widgets

Open **Administration > Layouts** (`#/manage/layouts`). Viewing requires `Configuration.Read`; saving requires `Configuration.Manage` and configuration must not be read-only. Add or copy a layout, choose the default, then arrange sections and widgets using rows, columns and spans. Every section has a **Width**: **Site width** (default) keeps the section inside the page container, **Full width** stretches it edge to edge across the viewport. The preview shows placement; Save applies all edits together. Each layout needs exactly one **Site content** widget, which displays the currently routed page.

The engine renders the entire public page: no header or footer exists outside the layout. New layouts contain full-width **Header**, site-width **Site content**, and full-width **Footer** sections. Header and Footer widgets are optional: remove, move or replace them with menus and custom widgets as needed. The Header provides branding, navigation and account actions; the Footer provides application information and footer links. Existing layouts automatically gain these widgets on load to preserve their appearance; save to persist the upgraded configuration. Account, management and restricted-access pages use a fixed shell. After saving, the editor shows the selected saved layout around the existing editor until you navigate away; other management pages stay unchanged.

Sections and widgets have a **CSS classes** field for space-separated class names from your site's stylesheet (up to 500 characters, for example `site-banner theme-dark`). Each name starts with an ASCII letter, underscore or hyphen and continues with letters, digits, underscores or hyphens. These classes style the rendered section or widget wrapper; they do not enable scripts, arbitrary stylesheets or unsanitized HTML. The editor keeps its placement controls independent of these classes.

Each widget placement also has independent **Borderless**, **No margin** and **No padding** switches. They remove the cell/widget frame's border (including rounded corners), outer margin or inner padding. They work with content, cards, messages, headers and footers, including site skins. Nested content, buttons, input controls and section/grid gaps retain their own styling; use section width and custom CSS classes for those gaps. All switches default to off for existing and new placements. The placement preview keeps its editing outlines so borderless cells remain visible; save to see the rendered appearance.

For **Page header** widgets, **Hamburger button position** chooses **Site default**, **Left** or **Right**. It affects the collapsed navigation button at the site's existing responsive breakpoint; desktop navigation and collapse behavior do not change. Site default preserves the current skin's placement. Each header placement can choose its own side; fixed management/account headers use the site default.

Widgets may share grid cells. Set **Overlap previous section (px)** to move a section upward by 0–2000 pixels; the first section has no preceding section, so its offset is ignored. **Layer** (-100 to 100) controls stacking separately for sections and for widgets within each section: larger numbers appear in front, and ties follow the list order. Section layers take precedence over widget layers across sections. The preview shows placement with placeholder heights, so actual overlap depends on the rendered content. On small screens sections with overlapping widgets retain their grid; other sections stack widgets in their configured order. Section offsets remain active on small screens.

Add latest news, articles or downloads, horizontal/vertical menus (by menu name), custom rich text or HTML, chat, a scheduled [Message](./managing-messages.md) (choose it in the widget's **Message** list; it shows a dismissible notice while it is live), or installed module widgets such as Forum posts/statistics. Unsafe HTML and scripts are removed; HTML editing is not a way to run JavaScript. Widget data respects the original content access rules.

Widgets use Bootstrap cards by default, with list and statistics styling where applicable. Site themes can customize their colors; the site content itself keeps its existing appearance.

Menu widgets keep parent and child links: horizontal menus open dropdowns, while vertical menus show indented submenus. Custom widgets support picture upload, paste and drop in the rich-text editor (PNG, JPEG, GIF or WebP, subject to the site image limit). Upload requires `Configuration.Manage`; images are stored in the layout VFS and served from `/layouts/images/{id}`. Uploaded pictures are attached to the layout configuration when saved. Rich-text alignment and the safe `ql-align-*` / `ql-image-*` HTML classes work on the public page too; arbitrary CSS classes and inline styles are still removed for safety.

Assign layouts to exact hash routes (`#/news`) or prefixes ending with `*` (`#/site/*`); the most specific match wins. Unassigned routes use the default. Account and administration pages use the simple layout, except for the editor preview after saving. Choose another default before deleting the default layout. Deletion is soft; developers can show deleted layouts, restore or permanently delete them. Save after each lifecycle action to persist it. A conflict means another editor saved first: reload before retrying.

Menu changes refresh mounted menus automatically without losing page content. Layout configuration is backed up as `layouts.json` in the configured data directory, separately from the database.

### Top-layer badges

Image badges sit flush against their selected page corner, without margins, padding or gaps between images. Text badges retain their inset and padding.

In the layout editor, use **Top-layer badges > Add badge** for notices such as "Under development" or a linked EU funding image. Each layout can have up to 20 badges. Enter **Text / image description** (required, up to 200 characters), choose a page corner and a width of 40–600 pixels, and optionally enter a link. Without an image, the text is displayed as a badge; with an uploaded PNG, JPEG, GIF or WebP, it becomes the image's accessible description. Removing the image returns to a text badge. Uploads use the same permission, size limits and `/layouts/images/{id}` storage as custom-widget images.

Badges overlay the whole public layout above its sections and menus. They are anchored to the page, not the viewport: top badges scroll away and bottom badges appear at the page bottom. Multiple badges in one corner stack in list order, and widths shrink on small screens. Keep badges small enough not to cover essential navigation. Links open in the current tab and accept local routes/paths or HTTP, HTTPS and mail links; scripts and protocol-relative URLs are rejected. Only badges themselves intercept clicks, not the surrounding transparent overlay. Fixed management, account and restricted-access pages do not show badges; the editor preview does. Save to persist changes; copying a layout copies its badges.

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
