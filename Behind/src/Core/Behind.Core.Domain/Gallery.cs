using Behind.Common;

namespace Behind.Core.Domain;

public sealed record Gallery(VersionedKey<Guid> Id, string Title, string Description)
{
    public const string EntityType = nameof(Gallery);
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 20_000;
    public const int MaxItems = 500;

    public Gallery(string title, string description)
        : this(new VersionedKey<Guid>(Guid.Empty), title, description)
    {
    }
}

public sealed record GalleryItem(VersionedKey<Guid> Id, string Description)
{
    public const string EntityType = nameof(GalleryItem);
    public const int MaxDescriptionLength = 2_000;

    public GalleryItem(string description)
        : this(new VersionedKey<Guid>(Guid.Empty), description)
    {
    }
}
