using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.WebApi.Markdown;

public static class MarkdownLinkExtensions
{
    private static readonly char[] SuffixStarts = ['?', '#'];

    extension(MarkdownLinks links)
    {
        public string Rewrite(string documentPath, string url) =>
            url.ToTarget(documentPath, links).Match(
                () => url,
                target => target.IsDocument ? links.Document(target) : links.Asset(target));

        private Option<MarkdownLinkTarget> Classify(string original, string resolved, string suffix)
        {
            if (links.Locales.Count == 0)
            {
                return Some(new MarkdownLinkTarget(original, resolved, suffix));
            }

            var (first, remainder) = resolved.SplitFirst();

            if (remainder.Length > 0 && links.Locales.Contains(first))
            {
                return Some(new MarkdownLinkTarget(original, remainder, suffix, first == links.Locale ? null : first));
            }

            var shared = new MarkdownLinkTarget(original, MarkdownLinks.SharedPrefix + resolved, suffix);

            return shared.IsDocument ? Some(shared) : None;
        }
    }

    extension(string url)
    {
        private Option<MarkdownLinkTarget> ToTarget(string documentPath, MarkdownLinks links)
        {
            if (url.Length == 0 || url[0] is '#' or '/' || Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                return None;
            }

            var split = url.IndexOfAny(SuffixStarts);
            var original = split < 0 ? url : url[..split];
            var suffix = split < 0 ? string.Empty : url[split..];

            return original.Length == 0
                ? None
                : Uri.UnescapeDataString(original)
                    .ResolveFrom(documentPath.BaseSegments(links.Locale))
                    .Bind(resolved => links.Classify(original, resolved, suffix));
        }

        private Option<string> ResolveFrom(IEnumerable<string> baseSegments)
        {
            var segments = new List<string>(baseSegments);

            foreach (var segment in url.Split('/'))
            {
                if (segment == "..")
                {
                    if (segments.Count == 0)
                    {
                        return None;
                    }

                    segments.RemoveAt(segments.Count - 1);
                }
                else if (segment is not ("." or ""))
                {
                    segments.Add(segment);
                }
            }

            return segments.Count == 0 ? None : Some(string.Join('/', segments));
        }

        private IEnumerable<string> BaseSegments(string? locale)
        {
            var shared = url.StartsWith(MarkdownLinks.SharedPrefix, StringComparison.Ordinal);
            var directories = (shared ? url[MarkdownLinks.SharedPrefix.Length..] : url).Split('/')[..^1];

            return locale is null || shared ? directories : [locale, ..directories];
        }

        private (string First, string Remainder) SplitFirst()
        {
            var index = url.IndexOf('/');

            return index < 0 ? (url, string.Empty) : (url[..index], url[(index + 1)..]);
        }
    }
}
