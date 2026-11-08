# Galleries

A **gallery** is a public collection of images with a title and an optional description. Every image can have its own description. Everybody can browse the galleries at **Galleries** (`#/galleries`) in the top menu.

## Browsing the galleries

- The **Galleries** page shows the published galleries as cards, newest first (by their creation), twelve per page, with page buttons at the bottom. A card shows the **thumbnail**, the title, the number of images, the author, the created or updated time (both taken from the [audit log](./audit-log.md)) and the description.
- The **Search** box finds the galleries whose title or description contains the words you type, ignoring upper and lower case.
- Opening a gallery shows its description and a grid of image thumbnails, each with its description when it has one. Clicking a thumbnail opens the image in a large viewer with its description below it, when it has one. Use the arrow buttons or the left and right arrow keys to go to the previous or next image, and the close button or **Esc** to close it.

## Managing galleries

You find the tools under **Administration > Galleries** (`#/manage/galleries`).

| Action | You need |
|--------|----------|
| See the list of galleries | the `Gallery.Read` grant |
| Create and edit galleries, upload images | the `Gallery.Write` grant |
| Delete galleries | the `Gallery.Delete` grant |
| See, restore and permanently delete deleted galleries | the `RawDatabase.Manage` grant (developers) |

Administrators have the first three grants by default. Users do not.

### Creating a gallery

**New gallery** opens the editor:

1. Enter the **title** (required) and, if you like, a **description**.
2. Choose the images in **Upload images**. You can select as many as you like at once, PNG, JPEG, GIF or WebP. They are uploaded in batches, the progress is shown, and the server generates a thumbnail for every image. Files that cannot be used are listed with the reason, the other images are added.
3. Optionally describe the images in their description boxes. Use the arrow buttons to change the order and the bin button to remove an image from the gallery.
4. **Save draft** keeps the gallery without publishing it. **Save and publish** shows it on the Galleries page; descriptions are optional, so you can publish with or without them. Uploaded images are only kept once the gallery is saved.

### The thumbnail of the gallery

The **Thumbnail** setting of the editor decides what the Galleries page shows:

- **Generate it from the images of the gallery** (default): a square collage of the first four images, created on the server whenever the gallery is saved.
- **Use an uploaded image**: choose a file in the thumbnail upload, it is scaled down on the server.

### Versions, drafts and the live version

Galleries are saved in **versions**, like [articles](./managing-articles.md#versions-drafts-and-the-live-version): every save creates a new version with its own list of images and descriptions, and nothing is overwritten.

- A gallery that was never published is a *Draft* and cannot be seen on the Galleries page.
- Saving a draft of a gallery that is already live keeps the live version, the list then shows a *Newer draft* badge.
- **Load into editor** copies an older version into the editor, **Make live** switches the live version to any version.

### Deleting and restoring

- **Delete** soft deletes a gallery. Nothing is lost.
- Developers see a **Show deleted** switch on the list. A deleted gallery shows **Restore** and **Delete permanently**, which removes the gallery with all its versions.

Everything you change here is recorded in the [audit log](./audit-log.md) with the title of the gallery and the description of the image instead of identifiers.
