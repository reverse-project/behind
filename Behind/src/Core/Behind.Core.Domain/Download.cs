using Behind.Common;

namespace Behind.Core.Domain;

public sealed record DownloadFolder(VersionedKey<Guid> Id, string Slug, string DisplayName)
{
    public const string EntityType = nameof(DownloadFolder);
    public const int MaxSlugLength = 100;
    public const int MaxDisplayNameLength = 200;

    public DownloadFolder(string slug, string displayName)
        : this(new VersionedKey<Guid>(Guid.Empty), slug, displayName)
    {
    }
}

public sealed record Download(VersionedKey<Guid> Id, string DisplayName)
{
    public const string EntityType = nameof(Download);
    public const int MaxDisplayNameLength = 200;

    public Download(string displayName)
        : this(new VersionedKey<Guid>(Guid.Empty), displayName)
    {
    }
}
