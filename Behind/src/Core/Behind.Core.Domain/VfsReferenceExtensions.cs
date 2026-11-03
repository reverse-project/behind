using System.Text.RegularExpressions;

namespace Behind.Core.Domain;

public static class VfsReferenceExtensions
{
    private const string GuidPattern = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

    extension(string? content)
    {
        public IReadOnlyList<Guid> ReferencedKeys(string url) =>
            string.IsNullOrEmpty(content)
                ? []
                : Regex.Matches(content, $"{Regex.Escape(url)}/({GuidPattern})")
                    .Select(match => Guid.Parse(match.Groups[1].Value))
                    .Distinct()
                    .ToList();
    }
}
