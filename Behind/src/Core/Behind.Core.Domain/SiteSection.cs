using System.Text.Json.Serialization;
using Behind.Common;

namespace Behind.Core.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<SiteSectionTheme>))]
public enum SiteSectionTheme
{
    Normal,
    Accented
}

[JsonConverter(typeof(JsonStringEnumConverter<SiteSectionImagePosition>))]
public enum SiteSectionImagePosition
{
    Left,
    Right
}

public sealed record SiteSection(
    VersionedKey<Guid> Id,
    SiteSectionTheme Theme,
    string Title,
    string Content,
    string ImageAlt,
    SiteSectionImagePosition ImagePosition)
{
    public const string EntityType = nameof(SiteSection);
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 100_000;
    public const int MaxImageAltLength = 300;

    public SiteSection(
        SiteSectionTheme theme,
        string title,
        string content,
        string imageAlt,
        SiteSectionImagePosition imagePosition)
        : this(new VersionedKey<Guid>(Guid.Empty), theme, title, content, imageAlt, imagePosition)
    {
    }
}
