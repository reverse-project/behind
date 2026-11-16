using System.Globalization;
using Behind.Core.Common.Email;

namespace Behind.Core.Common.Configuration;

public sealed record FrontendConfiguration(Uri? Url, Site Site, EmailAddress ContactEmail)
{
    public const string SectionName = "Frontend";
}

public sealed record Site(string? Title, string? Copyright, CultureInfo? DefaultLocale);