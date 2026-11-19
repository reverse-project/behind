using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Behind.Common.Configuration;

internal static class JsonIntegerExtensions
{
    extension(JsonNode? value)
    {
        public bool TryNormalizeInt64(out JsonNode? normalized)
        {
            normalized = null;
            if (value is not JsonValue number || number.GetValueKind() != JsonValueKind.Number)
            {
                return false;
            }

            var text = number.ToJsonString();
            var exponentIndex = text.IndexOfAny(['e', 'E']);
            var significand = exponentIndex < 0 ? text : text[..exponentIndex];
            var decimalIndex = significand.IndexOf('.');
            var fractionLength = decimalIndex < 0 ? 0 : significand.Length - decimalIndex - 1;
            var digits = significand.Replace(".", string.Empty).TrimStart('-').TrimStart('0');
            if (digits.Length == 0)
            {
                normalized = JsonValue.Create(0L);
                return true;
            }

            var exponent = 0;
            if (exponentIndex >= 0 && !int.TryParse(text[(exponentIndex + 1)..],
                    NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            {
                return false;
            }

            var significantDigits = digits.TrimEnd('0');
            var scale = (long)exponent - fractionLength + digits.Length - significantDigits.Length;
            if (scale < 0 || significantDigits.Length + scale > long.MaxValue.ToString(CultureInfo.InvariantCulture).Length)
            {
                return false;
            }

            var integer = (text[0] == '-' ? "-" : string.Empty) + significantDigits + new string('0', (int)scale);
            if (!long.TryParse(integer, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
            {
                return false;
            }

            normalized = JsonValue.Create(result);
            return true;
        }
    }
}
