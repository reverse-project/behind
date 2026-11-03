namespace Behind.Common;

public static class FileNameExtensions
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    extension(string? fileName)
    {
        public string ToSafeFileName(string fallback, int maxLength)
        {
            var name = (fileName ?? string.Empty).Split(PathSeparators).Last().Trim();
            var limited = name.Length > maxLength ? name[..maxLength] : name;

            return limited.Length == 0 ? fallback : limited;
        }
    }
}
