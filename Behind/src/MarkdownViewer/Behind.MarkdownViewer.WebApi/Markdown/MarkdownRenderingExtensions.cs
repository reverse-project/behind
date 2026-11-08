using System.Text;
using Behind.Common.WebApi;
using Behind.MarkdownViewer.Domain;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Behind.MarkdownViewer.WebApi.Markdown;

public static class MarkdownRenderingExtensions
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    extension(string markdown)
    {
        public MarkdownPage Render(string path, MarkdownLinks links)
        {
            var document = Markdig.Markdown.Parse(markdown, Pipeline);

            document.Descendants<LinkInline>()
                .Where(link => link.Url is not null)
                .ToList()
                .ForEach(link => link.Url = links.Rewrite(path, link.Url!));

            return new MarkdownPage(path, document.TitleFor(path), Markdig.Markdown.ToHtml(document, Pipeline).SanitizeMarkdown());
        }

        public string TitleOf(string path) => Markdig.Markdown.Parse(markdown, Pipeline).TitleFor(path);

        public IReadOnlyDictionary<string, string> LinkLabels() =>
            Markdig.Markdown.Parse(markdown, Pipeline)
                .Descendants<LinkInline>()
                .Where(link => !link.IsImage && link.Url is not null)
                .Select(link => (Target: link.Url!.ToLinkTarget(), Label: link.PlainText()))
                .Where(link => link.Label.Length > 0)
                .GroupBy(link => link.Target)
                .ToDictionary(group => group.Key, group => group.First().Label);
    }

    extension(MarkdownDocument document)
    {
        private string TitleFor(string path) =>
            document.Descendants<HeadingBlock>()
                .Where(heading => heading.Level == 1 && heading.Inline is not null)
                .Select(heading => heading.Inline!.PlainText())
                .FirstOrDefault(text => text.Length > 0)
            ?? Path.GetFileNameWithoutExtension(path);
    }

    extension(string url)
    {
        private string ToLinkTarget()
        {
            var end = url.IndexOfAny(['?', '#']);
            var path = Uri.UnescapeDataString(end < 0 ? url : url[..end]);

            return path.StartsWith("./", StringComparison.Ordinal) ? path[2..] : path;
        }
    }

    extension(ContainerInline inline)
    {
        private string PlainText() =>
            inline.Descendants()
                .Select(node => node switch
                {
                    LiteralInline literal => literal.Content.ToString(),
                    CodeInline code => code.Content,
                    _ => string.Empty
                })
                .Aggregate(new StringBuilder(), (text, part) => text.Append(part))
                .ToString()
                .Trim();
    }
}
