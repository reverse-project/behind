using Behind.Common.Database;

namespace Behind.Quotes.Database;

public interface IQuoteCommands
{
    public sealed record QuoteDraft(string Text, string Author);

    Task<QuoteSaveResult> Create(QuoteDraft draft, CancellationToken cancellationToken = default);

    Task<QuoteSaveResult> Update(Guid quoteId, QuoteDraft draft, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid quoteId, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid quoteId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid quoteId, CancellationToken cancellationToken = default);

    Task<bool> Ensure(Guid quoteId, QuoteDraft draft, CancellationToken cancellationToken = default);
}
