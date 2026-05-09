using System.Globalization;

namespace Behind.Core.Common.Configuration;

public sealed record FrontendConfiguration(Uri? Url, Site Site)
{
    public const string SectionName = "Frontend";
}

public sealed record Site(string? Title, string? Copyright, CultureInfo? DefaultLocale);