using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Forum.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Forum.Database.Commands;

public sealed class ForumUserModerationCommands(IForumContext context, IModuleUsers users)
    : VersionedCommands<ForumUserModeration, ForumUserModeration>(context)
{
    protected override ForumUserModeration Build(ForumUserModeration draft) => draft;

    protected override async Task<Option<string>> Validate(Guid key, ForumUserModeration draft, CancellationToken cancellationToken) =>
        !Enum.IsDefined(draft.Moderation) || (await users.Find([draft.UserId], cancellationToken)).Count == 0
            ? Some("unknownUser")
            : None;
}
