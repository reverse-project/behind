using Behind.Common;

namespace Behind.Core.Domain;

public enum MessageLevel
{
    Info,
    Success,
    Warning,
    Danger
}

public sealed record Message(
    VersionedKey<Guid> Id,
    string Name,
    string Title,
    string Content,
    MessageLevel Level,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt)
{
    public const string EntityType = nameof(Message);
    public const int MaxNameLength = 200;
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 100_000;
    public const int MaxLevelLength = 16;

    public Message(
        string name,
        string title,
        string content,
        MessageLevel level,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
        : this(new VersionedKey<Guid>(Guid.Empty), name, title, content, level, startsAt, endsAt)
    {
    }
}
