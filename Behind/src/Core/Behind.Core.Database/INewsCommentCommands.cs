using Behind.Common;

namespace Behind.Core.Database;

public interface INewsCommentCommands
{
    Task<Option<Guid>> Add(Guid newsId, Guid authorId, string content, CancellationToken cancellationToken = default);

    Task<bool> Update(Guid commentId, string content, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid commentId, CancellationToken cancellationToken = default);
}
