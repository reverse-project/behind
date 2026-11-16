# Downloads

The **Downloads** page is a library of files. The files are kept in **folders** and folders can contain other folders.

## Browsing and downloading

- The **Downloads** tab of the menu opens the page. On the left you see the **folder tree**: a closed folder has a closed folder icon, the folder you are in and the folders you opened have an open one. The arrow next to a folder opens or closes its subfolders.
- Choosing a folder shows its files in a table: the **name** (with an icon for the type of the file, a plain file icon when the type is unknown), the **size**, when it was **uploaded** and **who uploaded** it. When the name that is shown differs from the original file name, the file name is shown below it.
- Click the name of a file to download it.
- The address of a folder follows its place in the tree, for example `#/downloads/manuals/drivers` (`#/manage/downloads/manuals/drivers` on the management page) is the folder with the slug `drivers` in the folder with the slug `manuals`. You can bookmark or share it.

## Managing downloads

The public Downloads page only browses and downloads. You manage the downloads on a separate page, **Administration > Downloads** (`#/manage/downloads`), which has the same folder tree and file table plus the tools below. Users with the `Download.Read` grant also see a **Manage downloads** button on the public page that opens the same folder in the management page.

| You can | You need |
|---------|----------|
| See the tool in the administration page | the `Download.Read` grant |
| Create, edit and upload, rename files | the `Download.Write` grant |
| Delete folders and files | the `Download.Delete` grant |
| See, restore and permanently delete deleted folders and files | the `RawDatabase.Manage` grant (developers) |

### Folders

- **New folder** creates a folder inside the folder you are in (at the top when no folder is chosen). Fill in the **display name** shown in the tree and the **slug** used in the address. The slug is made from the display name until you type your own; it can only contain lowercase letters, digits and dashes and must be unique inside the folder.
- **Edit folder** changes the display name and the slug of the folder you are in. The address of the folder (and of its subfolders) changes with the slug.
- **Delete folder** deletes the folder you are in. Its subfolders and files are hidden together with it.

### Uploading files

1. Choose a folder.
2. Under **Upload files** choose one or more files. You can pick several at once.
3. Every chosen file gets a **display name**. It is the file name until you change it. Remove the files you do not want with **Remove**.
4. **Upload** sends the files one after the other. A file that is empty or larger than the allowed maximum (shown under the file chooser, set by the administrator of the server in `backend.json`) is marked and not uploaded; the others are.

Use **Rename** in the table to change the display name of an uploaded file later and **Delete** to delete it.

### Deleted folders and files

- **Delete** is soft: nothing is lost.
- Developers see a **Show deleted folders and files** switch. Deleted folders and files can be **restored** or **permanently deleted**. A folder can only be restored when the folder it was in is not deleted and no other folder in it uses its slug; a file can only be restored when its folder is not deleted. Permanently deleting a folder also removes its subfolders and files, and the content of the files, for good.

Everything you change here is recorded in the [audit log](./audit-log.md) with the display names of the folders and files.

## Short routes for folders

A deep folder such as `#/downloads/products/manuals` can get a short route like `#/manuals` with a [route alias](./managing-route-aliases.md).
