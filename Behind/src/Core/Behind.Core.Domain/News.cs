using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public enum NewsStatus
{
    Scheduled,
    Live,
    Expired
}

public sealed record News(
    VersionedKey<Guid> Id,
    string Title,
    string Summary,
    string Content,
    Option<DateTimeOffset> StartAt,
    Option<DateTimeOffset> EndAt,
    bool Highlighted = false)
{
    public const string EntityType = nameof(News);
    public const int MaxTitleLength = 200;
    public const int MaxSummaryLength = 20_000;
    public const int MaxContentLength = 1_000_000;

    public News(string title, string summary, string content, bool highlighted)
        : this(new VersionedKey<Guid>(Guid.Empty), title, summary, content, None, None, highlighted)
    {
    }
}

public static class NewsScheduleExtensions
{
    extension(Option<DateTimeOffset> startAt)
    {
        public NewsStatus StatusAt(Option<DateTimeOffset> endAt, DateTimeOffset now) =>
            startAt.Match(() => false, start => start > now)
                ? NewsStatus.Scheduled
                : endAt.Match(() => false, end => end <= now) ? NewsStatus.Expired : NewsStatus.Live;

        public bool IsValidSchedule(Option<DateTimeOffset> endAt) =>
            endAt.Match(
                () => true,
                end => startAt.Match(() => false, start => end > start));
    }
}

public sealed record NewsCategory(VersionedKey<Guid> Id, string Name)
{
    public const string EntityType = nameof(NewsCategory);
    public const int MaxNameLength = 100;

    public NewsCategory(string name) : this(new VersionedKey<Guid>(Guid.Empty), name)
    {
    }
}

public sealed record NewsComment(VersionedKey<Guid> Id, string Content)
{
    public const string EntityType = nameof(NewsComment);
    public const int MaxContentLength = 2_000;

    public NewsComment(string content) : this(new VersionedKey<Guid>(Guid.Empty), content)
    {
    }
}
