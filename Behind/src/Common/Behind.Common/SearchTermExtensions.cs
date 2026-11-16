using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public static class SearchTermExtensions
{
    extension(string? value)
    {
        public Option<string> ToSearchTerm(int maxLength)
        {
            var trimmed = value?.Trim() ?? string.Empty;

            return trimmed.Length == 0 ? None : Some(trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed);
        }
    }
}
