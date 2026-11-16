namespace Behind.Core.Domain;

public static class DisplayNameExtensions
{
    public const int MaxLength = 500;

    extension(string? displayName)
    {
        public string ToDisplayName() => displayName?.Trim() ?? string.Empty;
    }
}
