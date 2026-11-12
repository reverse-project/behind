namespace Behind.Core.Database;

public interface IChatStore
{
    sealed record Message(
        Guid Id, long Version, Guid AuthorId, string Text, Guid? ReplyToId,
        DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, bool IsAnonymous = false);

    sealed record Page(IReadOnlyList<Message> Items, Guid? NextCursor);

    enum SaveStatus { Saved, NotFound, Forbidden, Expired, Conflict, InvalidReply }

    sealed record SaveResult(SaveStatus Status, Guid? Id = null);

    Task<Page?> List(Guid? before, int size, CancellationToken cancellationToken);
    Task<IReadOnlyList<Message>> Find(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<SaveResult> Add(Guid authorId, string text, Guid? replyToId, CancellationToken cancellationToken, bool isAnonymous = false);
    Task<SaveResult> Update(Guid id, long version, Guid authorId, string text, CancellationToken cancellationToken, bool? isAnonymous = null);
    Task<bool> Delete(Guid id, CancellationToken cancellationToken);
}
