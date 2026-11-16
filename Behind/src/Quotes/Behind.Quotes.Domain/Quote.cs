using Behind.Common;

namespace Behind.Quotes.Domain;

public sealed record Quote(VersionedKey<Guid> Id, string Text, string Author)
{
    public const string EntityType = nameof(Quote);
    public const int MaxTextLength = 1_000;
    public const int MaxAuthorLength = 200;

    public Quote(string text, string author)
        : this(new VersionedKey<Guid>(Guid.Empty), text, author)
    {
    }
}
