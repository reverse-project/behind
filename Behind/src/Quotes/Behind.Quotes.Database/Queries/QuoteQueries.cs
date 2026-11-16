using Behind.Common;
using Behind.Common.Database;
using Behind.Quotes.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Quotes.Database.Queries;

public sealed class QuoteQueries(IQuotesContext context) : IQuoteQueries
{
    public async Task<PagedResult<IQuoteQueries.QuoteListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(quote => quote.Author, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(Ordered(matching))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<IQuoteQueries.QuoteListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IQuoteQueries.QuoteListing>>(rows.Select(ToListing).ToList())
                : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(quote => quote.Author, cancellationToken);
    }

    private static IQueryable<QuoteRow> Ordered(IQueryable<Quote> quotes) =>
        quotes
            .OrderBy(quote => quote.Author.ToLower())
            .ThenBy(quote => quote.Text.ToLower())
            .ThenBy(quote => EF.Property<Guid>(quote, VersionedColumns.Key))
            .Select(quote => new QuoteRow(
                EF.Property<Guid>(quote, VersionedColumns.Key),
                EF.Property<long>(quote, VersionedColumns.Version),
                quote.Text,
                quote.Author));

    private static IQuoteQueries.QuoteListing ToListing(QuoteRow row) =>
        new(new VersionedKey<Guid>(row.Key, row.Version), row.Text, row.Author);

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<Quote> Matching(Option<string> search, DeletionState state)
    {
        var latest = context.InDeletionState(context.Quotes, state).LatestOf(context.Quotes);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(quote => quote.Text.ToLower().Contains(term) || quote.Author.ToLower().Contains(term)));
    }

    private sealed record QuoteRow(Guid Key, long Version, string Text, string Author);
}
