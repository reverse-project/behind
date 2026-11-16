using Behind.Forum.Common;

namespace Behind.Forum.Domain;

public static class ModerationExtensions
{
    extension(ForumSettings settings)
    {
        public PublicationState InitialState(ModerationMode? board, ModerationMode? user)
        {
            var mode = settings.AllowUserOverrides && user.HasValue
                ? user.Value
                : settings.AllowBoardOverrides && board.HasValue
                    ? board.Value
                    : settings.Moderation;

            return mode == ModerationMode.Pre ? PublicationState.Pending : PublicationState.Published;
        }
    }
}
