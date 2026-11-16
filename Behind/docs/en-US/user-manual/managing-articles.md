# Articles

**Articles** are public pages with a title and a content, written by administrators in the browser, grouped in categories and searchable. Everybody can read them at **Articles** (`#/articles`) in the top menu. They are like the [news](./managing-news.md), without comments, schedule and highlight: an article is shown as soon as it is published and stays until it is deleted.

## Reading the articles

- The **Articles** page lists the published articles, newest first (by their creation), ten per page, with page buttons at the bottom.
- Every entry shows its title, its **short content**, the **author**, the time it was created (or, once it was edited, the time it was last updated: "Updated ..."), both taken from the [audit log](./audit-log.md), and its category.
- The buttons above the list filter the articles by **category**. **All** shows everything. While a category with a header image is selected, its image is shown above the list, and every entry of a category shows that image too.
- The **Search** box finds the articles whose **title** or **content** contains the words you type, ignoring upper and lower case. The search works together with the category filter. The short content is not searched.
- **Read more** (or the title) opens the article, which shows its **content** (the short content when no content was written), the author and the created or updated time.

## Managing articles

You find the tools under **Administration > Articles** (`#/manage/articles`) and **Administration > Article categories** (`#/manage/article-categories`).

| Action | You need |
|--------|----------|
| See the lists of articles and categories | the `Article.Read` grant |
| Create and edit articles and categories, upload images | the `Article.Write` grant |
| Delete articles and categories | the `Article.Delete` grant |
| See, restore and permanently delete deleted articles and categories | the `RawDatabase.Manage` grant (developers) |

Administrators have the first three grants by default. Users do not.

### Writing articles

**New article** opens the editor with:

- the **title** (required),
- the **category** (optional, the list of article categories to choose from),
- the **short content**, shown in the article list, and the **content**, shown when the article is opened. Both are written in the rich text editor of the [sites](./managing-sites.md#title-slug-and-content). Scripts, styles and unknown tags are removed when the article is saved.

The author is the signed in user who created the article. The content may be left empty: the opened article then shows the short content. The management list has a **Search** box on the title and the content, a letter filter and page buttons.

### Versions, drafts and the live version

Articles are saved in **versions**, like [news](./managing-news.md#versions-drafts-and-the-live-version): every save creates a new version and nothing is ever overwritten.

- **Save draft** stores a new version that is not shown. An article that was never published is a *Draft* in the list and cannot be seen on the Articles page.
- **Save and publish** stores a new version and makes it the **live** version, the one the Articles page shows.
- Saving a draft of an article that is already live keeps the live version, the list then shows a *Newer draft* badge.
- **Load into editor** copies an older version into the editor, **Make live** switches the live version to any version, which is also how you roll back.

### Categories

Article categories are separate from the news categories. A category has a **name**, unique among the article categories (ignoring upper and lower case), and an optional **header image**. An article belongs to at most one category. Deleting a category keeps its articles, which are shown without a category.

### Deleting and restoring

- **Delete** soft deletes an article or a category. Nothing is lost.
- Developers see a **Show deleted** switch on both lists. A deleted entry shows **Restore** and **Delete permanently**. A category can only be restored while no other article category uses its name.

Everything you change here is recorded in the [audit log](./audit-log.md) with the title of the article or the name of the category instead of identifiers.
