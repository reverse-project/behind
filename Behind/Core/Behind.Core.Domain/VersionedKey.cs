using static LaYumba.Functional.F;
using System.Text.Json;

namespace Behind.Core.Domain;

public sealed record VersionedKey<TKey>(TKey Key, long Version = 0)
{
    public static implicit operator string?(VersionedKey<TKey>? versionedKey) => versionedKey?.Key?.ToString();

    public static implicit operator VersionedKey<TKey>?(string? value)
    {
        if (value is null)
        {
            return null;
        }

        TKey KeyParser(string keyValue) => JsonSerializer.Deserialize<TKey>(keyValue)
                                           ?? throw new ArgumentException(nameof(value));

        if (!value.Contains('.'))
        {
            return new VersionedKey<TKey>(KeyParser(value));
        }
        
        string[] parts = value.Split('.');

        if (parts.Length > 2)
        {
            throw new ArgumentException(nameof(value));
        }

        var parsedKey = KeyParser(parts[0]);
        long version = 0;

        if (parsedKey is null || parts.Length == 2 && !long.TryParse(parts[1], out version))
        {
            throw new ArgumentException(nameof(value));
        }

        return parts.Length == 1 ? new VersionedKey<TKey>(parsedKey) : new VersionedKey<TKey>(parsedKey, version);
    }

    public static VersionedKey<T> Parse<T>(string value) => value!;

    public static bool TryParse<T>(string value, out VersionedKey<T>? versionedKey)
    {
        bool success = false;
        VersionedKey<T>? parsedKey = null;
        
        Try(() => (VersionedKey<T>?)value)()
            .Match(
            exception => success = false,
            v =>
            {
                parsedKey = v;
                success = true;
            });

        versionedKey = parsedKey;
        return success;
    }
    public override string ToString() => $"{Key}:{Version}";
}