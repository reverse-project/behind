using Behind.Forum.Common;

namespace Behind.Forum.Domain;

public sealed record ForumBoard(string Name, string Description, ModerationMode? Moderation, Guid? CategoryId = null);
