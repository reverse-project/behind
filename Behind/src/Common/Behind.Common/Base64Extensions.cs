using System.Text;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public static class Base64Extensions
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    extension(string? value)
    {
        public Option<byte[]> FromBase64()
        {
            var normalized = (value ?? string.Empty).Trim().Replace('-', '+').Replace('_', '/');
            var padded = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            var buffer = new byte[padded.Length / 4 * 3];

            return normalized.Length > 0 && Convert.TryFromBase64String(padded, buffer, out var written)
                ? Some(buffer[..written])
                : None;
        }

        public Option<string> FromBase64Text() =>
            value.FromBase64().Bind(bytes => Try(() => StrictUtf8.GetString(bytes))
                .Run()
                .Match<Option<string>>(_ => None, text => Some(text!)));
    }
}
