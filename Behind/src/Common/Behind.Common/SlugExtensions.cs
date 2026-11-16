using System.Globalization;
using System.Text;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public static class SlugExtensions
{
    private const char Separator = '-';

    extension(string? value)
    {
        public Option<string> ToSlug(int maxLength)
        {
            var slug = (value ?? string.Empty)
                .Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .Aggregate(new StringBuilder(), (builder, character) => builder.AppendSlugCharacter(character))
                .ToString()
                .Trim(Separator);

            var limited = (slug.Length > maxLength ? slug[..maxLength] : slug).Trim(Separator);

            return limited.Length == 0 ? None : Some(limited);
        }
    }

    extension(StringBuilder builder)
    {
        private StringBuilder AppendSlugCharacter(char character)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                return builder.Append(char.ToLowerInvariant(character));
            }

            return builder.Length > 0 && builder[^1] != Separator ? builder.Append(Separator) : builder;
        }
    }
}
