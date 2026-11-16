namespace Behind.Core.Domain;

public static class UserSettingKeys
{
    public const string Locale = "locale";
    public const int MaxKeyLength = 64;
    public const int MaxValueLength = 256;

    public static IReadOnlySet<string> Known { get; } = new HashSet<string>(StringComparer.Ordinal) { Locale };
}
