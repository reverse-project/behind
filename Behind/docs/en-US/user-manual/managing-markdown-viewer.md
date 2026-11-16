# Documentation (Markdown Viewer)

**Documentation** is the Markdown Viewer module of Behind: it shows the Markdown (`.md`) files of a directory as a documentation website and can generate the same documentation as static HTML pages. It does not use the file storage (VFS) of the database: it works directly with two directories of the server.

Everybody can read the documentation under **Documentation** in the main menu (`#/docs`). The static pages are generated under **Administration > Documentation** (`#/manage/markdown-viewer`).

| Action | You need |
|--------|----------|
| Read the documentation | nothing, it is open |
| Open the generator page | the `MarkdownViewer.Read` grant |
| Generate the static pages | the `MarkdownViewer.Write` grant |
| Change the directories | the `Configuration.Manage` grant (developers) |

Administrators have the `MarkdownViewer` grants by default.

## Setting the directories

The module is configured in `markdownviewer.json`, which has its own tab on the [Settings](./managing-settings.md) page:

| Setting | Meaning |
|---------|---------|
| **SourceDirectory** | The directory with the Markdown files, or with one folder per language (see [Languages](#languages)). Empty or missing: the documentation is not available. |
| **OutputDirectory** | The directory the static pages are generated into. It must not contain the source directory and must not be inside it. |
| **Title** | The title of the documentation, shown in the navigation and the generated pages. |
| **DefaultDocument** | The document shown first, for example `README.md`. When it does not exist the first document is shown. |

Relative directories are resolved against the folder of the application. A change applies immediately, the application does not need to be restarted. Use absolute paths, or paths mounted into the container, in production.

## Reading the documentation

The page lists the documents of the source directory (sub folders included, hidden files and folders and symbolic links are ignored) in a navigation. The title above the navigation is the home page (the **DefaultDocument**) and is not listed again. Every top level folder is a section: its name is the text of the link to the folder's `README.md` in the home document (for example **Developer documentation** and **User manual**), otherwise the title of that `README.md`, otherwise the folder name. The section links to its `README.md`, followed by the other documents of the folder. The generated static pages use the same navigation. The title of a document is its first `# heading`, or its file name. Links to other `.md` files open them in the viewer, images next to the documents (`png`, `jpg`, `gif`, `webp`, `svg`, `ico`) are shown, and links to headings of the same page scroll to them. Raw HTML inside a Markdown file is shown as text and unsafe links are removed.

## Languages

The documentation follows the language selector of the application. A source directory can hold one folder per language, named with the locale code (`en-US`, `hu-HU`: two or three lowercase letters, a dash and two capital letters):

```text
docs/
  en-US/
    README.md
    guide/setup.md
  hu-HU/
    README.md
    guide/setup.md
```

- The viewer shows the documents of the language chosen in the selector. When a document does not exist in that language it shows the same document in the default language (`Frontend › Site › DefaultLocale` of `frontend.json`), then in the first language (alphabetically), and a notice tells which language is shown. Images are taken from the same language folder, with the same fallback.
- Links between documents work inside a language folder and across languages (`../hu-HU/README.md` opens that language, whatever language is selected). Images stay inside their language folder.
- Markdown files directly in the source directory (not in a language folder) are shared pages, for example an overview of all languages. Language documents can link to them (`../README.md`) and they can link to the language folders (`en-US/README.md`); they are not listed in the navigation. Other folders (for example `api`) are never taken for languages.
- Without language folders the source directory is a single language and everything works as described above.
- The generator writes one site per language into `<output>/<locale>/...` with a language switcher in the header of every page (it links to the same document in the other language, or to its start page when it is missing). The shared pages are written to the root of the output (`<output>/README.html`). `<output>/index.html` sends the visitor to the language of the browser, or to the default language. A language folder without documents is skipped.

## Generating static pages

1. Set the **SourceDirectory** and the **OutputDirectory** in `markdownviewer.json`.
2. Open **Administration > Documentation** and choose **Generate static pages**.

For every Markdown file an HTML page with the same path is written (`guide/setup.md` becomes `guide/setup.html`) with the navigation, links between the pages, the `markdownviewer.css` stylesheet and the images of the source directory. `index.html` redirects to the default document unless the documentation has an `index.md` (with languages, every language folder gets its own `index.html`). The generator remembers what it wrote (`.markdownviewer-generated`), so pages of deleted documents are removed on the next run, and files it did not write are never touched. The result tells how many pages and files were written and how many outdated files were removed.

If the directories are not usable the generator tells why instead of writing anything: the source directory is not set or does not exist, the output directory is not set, or the two directories contain each other.
