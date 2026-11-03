using System.Text.RegularExpressions;

namespace Behind.Core.Domain;

public static partial class MenuUrlExtensions
{
    private static readonly IReadOnlySet<string> SafeSchemes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto", "tel" };

    [GeneratedRegex(@"^([A-Za-z][A-Za-z0-9+.\-]*):")]
    private static partial Regex SchemePattern();

    extension(string? url)
    {
        public bool IsSafeMenuUrl =>
            !string.IsNullOrEmpty(url)
            && url.Length <= MenuItem.MaxUrlLength
            && !url.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            && SchemePattern().Match(url) is var scheme
            && (!scheme.Success || SafeSchemes.Contains(scheme.Groups[1].Value));
    }
}
