using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public static class MessageExtensions
{
    private static readonly IReadOnlyDictionary<string, MessageLevel> Levels =
        Enum.GetValues<MessageLevel>().ToDictionary(level => level.Key, level => level, StringComparer.OrdinalIgnoreCase);

    extension(MessageLevel level)
    {
        public string Key => level.ToString().ToLowerInvariant();
    }

    extension(string? key)
    {
        public Option<MessageLevel> ToMessageLevel() =>
            key is not null && Levels.TryGetValue(key.Trim(), out var level) ? Some(level) : None;
    }

    extension(DateTimeOffset? startsAt)
    {
        public bool IsValidMessageSchedule(DateTimeOffset? endsAt) =>
            startsAt is not { } start || endsAt is not { } end || end > start;

        public bool IsMessageLiveAt(DateTimeOffset? endsAt, DateTimeOffset now) =>
            (startsAt is not { } start || start <= now) && (endsAt is not { } end || end > now);
    }

    extension(Message message)
    {
        public bool HasValidSchedule => message.StartsAt.IsValidMessageSchedule(message.EndsAt);

        public bool IsLiveAt(DateTimeOffset now) => message.StartsAt.IsMessageLiveAt(message.EndsAt, now);
    }
}
