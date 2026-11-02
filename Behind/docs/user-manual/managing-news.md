# News

**News** is a public list of announcements that administrators write in the browser. Everybody can read it at **News** (`#/news`) in the top menu, and signed in users can comment on it.

## Reading the news

- The **News** page lists the news that is running now, newest first (by its start when it has a schedule, by its creation otherwise), ten per page, with page buttons at the bottom.
- Every entry shows its title, its **short content**, the **author**, the time it was created (or, once it was edited, the time it was last updated: "Updated ..."), both taken from the [audit log](./audit-log.md), its category and the number of comments.
- A **highlighted** news is shown at the top of the first page, with a *Highlighted* badge and a coloured border, before the newer news.
- The buttons above the list filter the news by **category**. **All** shows everything. While a category with a header image is selected, its image is shown above the list, and every entry of a category shows that image too.
- **Read more** (or the title) opens the news, which shows its **content** (the short content when no content was written), the author, the created or updated time and the comments.

## Comments

- Signed in users write a comment under the news and send it with **Comment**. Comments are plain text of up to 2000 characters, listed oldest first with page buttons.
- You can **edit** and delete your own comments. **Edit** opens the comment in place, **Save** stores it as a new version of the comment and the comment then shows *edited* with the time of the change (taken from the [audit log](./audit-log.md)). Holders of the `News.Manage` grant (administrators, and the future moderators) can edit any comment, holders of the `News.Delete` grant can delete any comment. A deleted comment is soft deleted, see below.
- Without being signed in you can read the comments and are asked to sign in to write one.

## Managing news

You find the tools under **Administration > News** (`#/manage/news`) and **Administration > News categories** (`#/manage/news-categories`).

| Action | You need |
|--------|----------|
| See the lists of news and categories | the `News.Read` grant |
| Create and edit news and categories, upload images | the `News.Write` grant |
| Delete news and categories, and delete any comment | the `News.Delete` grant |
| Edit any comment | the `News.Manage` grant |
| See, restore and permanently delete deleted news and categories | the `RawDatabase.Manage` grant (developers) |

Administrators have the first three grants by default. Users do not.

### Writing news

**New news** opens the editor with:

- the **title** (required),
- the **category** (optional, the list of categories to choose from),
- the **short content**, shown in the news list, and the **content**, shown when the news is opened. Both are written in the rich text editor of the [sites](./managing-sites.md#title-slug-and-content): headings, bold, italic, lists, quotes, code blocks, links and uploaded images (PNG, JPEG, GIF or WebP, up to the image limit of the sites, 5 MB by default). Scripts, styles and unknown tags are removed when the news is saved.

Scheduling is **optional**. By default a news has no schedule: it is shown as soon as it is published and stays until it is deleted. Switch on **Schedule this news** to set a **Start** (required once scheduling is on, filled with the current time) and an optional **End**. A scheduled news is shown on the News page, can be opened and can be commented only from its start until its end (it stays hidden before the start and disappears at the end), and the end must be after the start. Leave the end empty to keep the news until it is deleted. A news that is not running is still found by managers, so it can be edited, extended or deleted.

The **Highlighted** switch flags the news as the highlighted one. Only one news can be highlighted: publishing a highlighted version removes the highlight of the news that had it, and deleting the highlighted news removes its highlight (restoring it does not bring it back).

The author is the signed in user who created the news. The content may be left empty: the opened news then shows the short content. The list has a **Search** box on the title, a letter filter and page buttons.

### Versions, drafts and the live version

News are saved in **versions**, like [sites](./managing-sites.md#versions-drafts-and-the-live-version): every save creates a new version and nothing is ever overwritten.

- **Save draft** stores a new version that is not shown. A news that was never published is a *Draft* in the list and cannot be seen on the News page.
- **Save and publish** stores a new version and makes it the **live** version, the one the News page shows.
- Saving a draft of a news that is already live keeps the live version, the list then shows a *Newer draft* badge.
- **Load into editor** copies an older version into the editor, saving creates a new version based on it. **Make live** switches the live version to any version, which is also how you roll back.
- The administration list shows the latest version and which version is live, the badges (*Draft*, *Newer draft*, *Scheduled*, *Expired*, *Highlighted*) describe the live version and its start and end.
- The *Created* and *Updated* times on the News page are the time version 1 was saved and the time the shown version was saved, taken from the [audit log](./audit-log.md).

### Categories

A category has a **name**, unique among the categories (ignoring upper and lower case), and an optional **header image**. Choose the image with the file field of the category form: it is uploaded at once and shown as a preview, **Remove image** drops it. **Edit** loads a category into the form, and **Save** stores it.

A news belongs to at most one category. Deleting a category keeps its news, which are shown without a category.

### Deleting and restoring

- **Delete** soft deletes a news (with its comments) or a category. Nothing is lost.
- Developers see a **Show deleted** switch on both lists. A deleted entry shows **Restore** and **Delete permanently**. Restoring a news also restores its comments. A category can only be restored while no other category uses its name. Deleting a category permanently keeps its news, without a category.

Everything you change here is recorded in the [audit log](./audit-log.md) with the title of the news, the name of the category or the author of the comment instead of identifiers.
