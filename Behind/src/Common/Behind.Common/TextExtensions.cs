namespace Behind.Common;

public static class TextExtensions
{
    extension(string? value)
    {
        public string ToRequiredText() => value?.Trim() ?? string.Empty;

        public string? ToOptionalText() => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
