using System.Data;
using Behind.Common;
using Behind.Common.Database;
using Microsoft.EntityFrameworkCore;
using Behind.Contact.Domain;

namespace Behind.Contact.Database.Commands;

public sealed class ContactFormCommands(IContactContext context) : VersionedCommands<ContactForm, ContactForm>(context)
{
    protected override ContactForm Build(ContactForm draft) => draft;

    public async Task<VersionedSaveResult> UpdateExpected(Guid key, long version, ContactForm draft, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var latest = await context.ContactForms.LatestVersionAsync(key, cancellationToken);
        if (latest == 0)
        {
            return new VersionedSaveResult.NotFound();
        }
        if (latest != version)
        {
            return new VersionedSaveResult.VersionConflict();
        }
        var id = new VersionedKey<Guid>(key, version + 1);
        await context.ContactForms.AddAsync(draft, cancellationToken);
        context.Context.Entry(draft).StampVersionedKey(key, id.Version);
        if (context.Context is IContentLocaleStore { ContentLocaleSpecified: true } locales)
        {
            await locales.StageContentLocale(nameof(ContactForm), key, locales.SelectedContentLocale, cancellationToken);
        }
        await context.Context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new VersionedSaveResult.Saved(id);
    }
}

public sealed class ContactRequestCommands(IContactContext context) : VersionedCommands<ContactRequest, ContactRequest>(context)
{
    protected override ContactRequest Build(ContactRequest draft) => draft;

    public async Task SaveExpected(Guid key, long version, ContactRequest draft, CancellationToken cancellationToken)
    {
        await context.ContactRequests.AddAsync(draft, cancellationToken);
        context.Context.Entry(draft).StampVersionedKey(key, version + 1);
        await context.Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HardDeleteResult> HardDeleteChecked(Guid key, CancellationToken cancellationToken)
    {
        if (!await context.Context.HasDeletedRowsAsync(context.ContactRequests.OfKey(key), cancellationToken))
        {
            return HardDeleteResult.NotFound;
        }
        await context.Context.MarkHardDeletedAsync(context.ContactRequests.OfKey(key), cancellationToken);
        await context.Context.SaveChangesAsync(cancellationToken);
        return HardDeleteResult.Deleted;
    }
}
