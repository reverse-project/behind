using Behind.Common.Database;
using Behind.Quotes.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Quotes.WebApi.Endpoints;

public static class QuoteResponseExtensions
{
    public const string VersionConflictReason = "version-conflict";

    extension(IQuoteQueries.QuoteListing quote)
    {
        public QuoteItem ToItem() => new(quote.Id.Key, quote.Text, quote.Author);

        public ManagedQuote ToManaged(DeletionState state) =>
            new(quote.Id.Key, quote.Text, quote.Author, state == DeletionState.Deleted);
    }

    extension(QuoteSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                QuoteSaveResult.Saved saved => Results.Json(new QuoteSavedResponse(saved.Id.Key, saved.Id.Version)),
                QuoteSaveResult.NotFound => Results.NotFound(),
                _ => Results.Conflict(new QuoteConflictResponse(VersionConflictReason))
            };
    }

    extension(bool found)
    {
        public IResult ToNoContentResult() => found ? Results.NoContent() : Results.NotFound();
    }
}
