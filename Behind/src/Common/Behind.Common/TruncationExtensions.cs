namespace Behind.Common;

public static class TruncationExtensions
{
    private const string Ellipsis = "…";

    extension(string value)
    {
        public string Shortened(int maxLength) =>
            maxLength <= Ellipsis.Length || value.Length <= maxLength
                ? value
                : $"{value[..(maxLength - Ellipsis.Length)].TrimEnd()}{Ellipsis}";
    }
}
