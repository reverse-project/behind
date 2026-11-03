namespace Behind.Core.Common.Configuration;

public sealed record CommentsConfiguration
{
    public const string SectionName = $"{BackendConfiguration.SectionName}:Comments";
    public const string NewsEntity = "News";

    public bool News { get; init; } = true;

    public bool IsEnabled(string entity) =>
        entity switch
        {
            NewsEntity => News,
            _ => false
        };
}
