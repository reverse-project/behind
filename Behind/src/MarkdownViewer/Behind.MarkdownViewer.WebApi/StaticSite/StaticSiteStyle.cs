namespace Behind.MarkdownViewer.WebApi.StaticSite;

public static class StaticSiteStyle
{
    public const string Css = """
        :root { color-scheme: light dark; --accent: #0d6efd; --border: rgba(127, 127, 127, .35); }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; line-height: 1.6; }
        header { padding: .75rem 1.25rem; border-bottom: 1px solid var(--border); font-weight: 600; }
        header { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: .5rem; }
        header a { color: inherit; text-decoration: none; }
        .languages { display: flex; gap: .75rem; font-weight: 400; font-size: .9em; }
        .languages a[aria-current] { font-weight: 600; }
        .languages a[href]:hover { color: var(--accent); }
        .layout { display: flex; flex-wrap: wrap; align-items: flex-start; }
        nav { flex: 0 0 18rem; max-width: 100%; padding: 1rem 1.25rem; border-right: 1px solid var(--border); }
        nav ul { list-style: none; margin: 0; padding: 0; }
        nav li { margin: .15rem 0; }
        nav li.section { margin-top: .75rem; font-weight: 600; }
        nav li.section:first-child { margin-top: 0; }
        nav li.depth-0 { padding-left: 1rem; }
        nav li.depth-1 { padding-left: 2rem; }
        nav li.depth-2 { padding-left: 3rem; }
        nav li.depth-3 { padding-left: 4rem; }
        nav a { color: inherit; text-decoration: none; }
        nav a:hover, nav a[aria-current="page"] { color: var(--accent); }
        main { flex: 1 1 30rem; min-width: 0; max-width: 60rem; padding: 1rem 1.5rem 3rem; }
        main img { max-width: 100%; height: auto; }
        main pre { overflow: auto; padding: .75rem 1rem; border: 1px solid var(--border); border-radius: .375rem; }
        main code { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: .9em; }
        main table { border-collapse: collapse; display: block; overflow-x: auto; }
        main th, main td { border: 1px solid var(--border); padding: .35rem .65rem; }
        main blockquote { margin-left: 0; padding-left: 1rem; border-left: .25rem solid var(--border); }
        main a { color: var(--accent); }
        """;
}
