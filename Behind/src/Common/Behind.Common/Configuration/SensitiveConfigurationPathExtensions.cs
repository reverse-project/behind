using System.Text.RegularExpressions;

namespace Behind.Common.Configuration;

public static partial class SensitiveConfigurationPathExtensions
{
    private const char PathSeparator = ':';
    private const string ConnectionStringsSection = "ConnectionStrings";

    [GeneratedRegex("password|secret|signingkey|apikey|token|credential", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveName();

    extension(string path)
    {
        public bool IsSensitiveConfigurationPath
        {
            get
            {
                var segments = path.Split(PathSeparator);

                return segments[0] == ConnectionStringsSection || SensitiveName().IsMatch(segments[^1]);
            }
        }
    }
}
