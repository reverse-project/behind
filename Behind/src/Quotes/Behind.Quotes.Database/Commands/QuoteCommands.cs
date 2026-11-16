using Behind.Common;
using Behind.Common.Database;
using Behind.Quotes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Quotes.Database.Commands;

public sealed class QuoteCommands(IQuotesContext context) : IQuoteCommands
{
    private DbContext Database => context.Context;

    public Task<QuoteSaveResult> Create(
        IQuoteCommands.QuoteDraft draft,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, draft, cancellationToken);

    public async Task<QuoteSaveResult> Update(
        Guid quoteId,
        IQuoteCommands.QuoteDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = await context.Quotes.LatestVersionAsync(quoteId, cancellationToken);

        return latest == 0
            ? new QuoteSaveResult.NotFound()
            : await AddVersion(quoteId, latest, draft, cancellationToken);
    }

    public async Task<bool> Delete(Guid quoteId, CancellationToken cancellationToken = default)
    {
        var deleted = await Database.MarkSoftDeletedAsync(context.Quotes.OfKey(quoteId), cancellationToken);

        await Database.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<bool> Restore(Guid quoteId, CancellationToken cancellationToken = default)
    {
        var restored = await Database.MarkRestoredAsync(context.Quotes.OfKey(quoteId), cancellationToken);

        await Database.SaveChangesAsync(cancellationToken);

        return restored > 0;
    }

    public async Task<HardDeleteResult> HardDelete(Guid quoteId, CancellationToken cancellationToken = default)
    {
        var deleted = await Database.MarkHardDeletedAsync(context.Quotes.OfKey(quoteId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        try
        {
            await Database.SaveChangesAsync(cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            Database.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    public async Task<bool> Ensure(
        Guid quoteId,
        IQuoteCommands.QuoteDraft draft,
        CancellationToken cancellationToken = default) =>
        !await ExistsInAnyState(quoteId, cancellationToken)
        && await AddVersion(quoteId, 0, draft, cancellationToken) is QuoteSaveResult.Saved;

    private async Task<bool> ExistsInAnyState(Guid quoteId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.Quotes.OfKey(quoteId).AnyAsync(cancellationToken);
    }

    private async Task<QuoteSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        IQuoteCommands.QuoteDraft draft,
        CancellationToken cancellationToken)
    {
        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var quote = new Quote(draft.Text, draft.Author);

        await context.Quotes.AddAsync(quote, cancellationToken);
        Database.Entry(quote).StampVersionedKey(id.Key, id.Version);

        try
        {
            await Database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Database.ChangeTracker.Clear();

            return new QuoteSaveResult.VersionConflict();
        }

        return new QuoteSaveResult.Saved(id);
    }
}
