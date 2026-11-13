namespace Behind.Core.Database;

public interface IReportConversations
{
    Task<Guid> CreateReport(Guid reporterId, string subject, string content, CancellationToken cancellationToken);

    Task AppendReportMessage(Guid conversationId, Guid authorId, string content, CancellationToken cancellationToken);

    Task DeleteReportConversation(Guid conversationId, CancellationToken cancellationToken);
}
