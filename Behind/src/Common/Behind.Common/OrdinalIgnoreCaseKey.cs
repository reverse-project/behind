using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Behind.Common;

public static class OrdinalIgnoreCaseKey
{
    public static string Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            normalized.Append(Fold(rune));
        }

        return normalized.ToString();
    }

    private static readonly Lazy<IReadOnlyList<(string Source, string Target)>> MappingValues = new(CreateMappings);

    public static IReadOnlyList<(string Source, string Target)> Mappings => MappingValues.Value;

    private static readonly Lazy<string> VersionValue = new(() =>
    {
        var version = CultureInfo.InvariantCulture.CompareInfo.Version;
        var mappings = string.Join("|", Mappings.Select(pair => $"{pair.Source}:{pair.Target}"));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mappings)));
        return $"{RuntimeInformation.FrameworkDescription}:{version.FullVersion}:{version.SortId}:{fingerprint}";
    });

    public static string Version => VersionValue.Value;

    private static string Fold(Rune rune)
    {
        var original = rune.ToString();
        var upper = Rune.ToUpperInvariant(rune).ToString();
        return string.Equals(original, upper, StringComparison.OrdinalIgnoreCase) ? upper : original;
    }

    private static IReadOnlyList<(string Source, string Target)> CreateMappings()
    {
        var mappings = new List<(string Source, string Target)>();
        for (var scalar = 0; scalar <= 0x10ffff; scalar++)
        {
            if (Rune.TryCreate(scalar, out var rune))
            {
                var original = rune.ToString();
                var folded = Fold(rune);
                if (original != folded)
                {
                    mappings.Add((original, folded));
                }
            }
        }

        return mappings.AsReadOnly();
    }
}
