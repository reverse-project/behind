using Behind.Common;
using Behind.Common.Database;

namespace Behind.Quotes.Database;

public interface IQuoteQueries
{
    public sealed record QuoteListing(VersionedKey<Guid> Id, string Text, string Author);

    Task<PagedResult<QuoteListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);
}
