using System.Text.Json.Serialization;

namespace Behind.Forum.Common;

[JsonConverter(typeof(JsonStringEnumConverter<ModerationMode>))]
public enum ModerationMode
{
    Post,
    Pre
}

public sealed record ForumSettings
{
    public const string SectionName = "Forum";

    public ModerationMode Moderation { get; init; } = ModerationMode.Post;

    public bool AllowBoardOverrides { get; init; }

    public bool AllowUserOverrides { get; init; }
}
