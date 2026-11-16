namespace Behind.Common;

public static class LetterBucketExtensions
{
    public const string Other = "#";

    public static readonly IReadOnlyList<string> All =
        [..Enumerable.Range('A', 26).Select(letter => ((char)letter).ToString()), Other];

    public static string Alphabet => string.Concat(All.Take(26));

    public static Option<string> ParseLetterBucket(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;

        return All.Find(bucket => bucket == normalized);
    }

    extension(string? label)
    {
        public string ToLetterBucket()
        {
            var first = label?.FirstOrDefault().ToString().ToUpperInvariant() ?? string.Empty;

            return Alphabet.Contains(first) && first.Length == 1 ? first : Other;
        }
    }
}
