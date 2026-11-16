using Behind.Common.WebApi;

namespace Behind.Quotes.WebApi.Endpoints;

public sealed record QuoteItem(Guid Id, string Text, string Author);

public sealed record QuotesResponse(PageResponse<QuoteItem> Quotes);

public sealed record ManagedQuote(Guid Id, string Text, string Author, bool IsDeleted);

public sealed record QuoteManageResponse(PageResponse<ManagedQuote> Quotes, IReadOnlyList<string> Letters);

public sealed record QuoteSavedResponse(Guid Id, long Version);

public sealed record QuoteConflictResponse(string Reason);
