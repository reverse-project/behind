using Behind.Common;
using Behind.Quotes.Database;
using Behind.Quotes.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Quotes.WebApi.Endpoints;

public static class QuoteDraftExtensions
{
    extension(QuoteEndpoints.SaveQuoteRequest request)
    {
        public Option<IQuoteCommands.QuoteDraft> ToDraft()
        {
            var text = (request.Text ?? string.Empty).Trim();
            var author = (request.Author ?? string.Empty).Trim();

            return text.Length == 0 || author.Length == 0 || text.Length > Quote.MaxTextLength || author.Length > Quote.MaxAuthorLength
                ? None
                : Some(new IQuoteCommands.QuoteDraft(text, author));
        }
    }
}
