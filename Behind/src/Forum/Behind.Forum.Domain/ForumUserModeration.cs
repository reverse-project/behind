using Behind.Forum.Common;

namespace Behind.Forum.Domain;

public sealed record ForumUserModeration(Guid UserId, ModerationMode Moderation);
