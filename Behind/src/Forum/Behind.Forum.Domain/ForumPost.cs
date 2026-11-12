using System.Text.Json.Serialization;

namespace Behind.Forum.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<PublicationState>))]
public enum PublicationState
{
    Published,
    Pending,
    Rejected
}

public sealed record ForumPost(
    Guid BoardId,
    Guid? TopicId,
    Guid AuthorId,
    string Title,
    string Body,
    PublicationState State,
    bool IsAnonymous = false);
