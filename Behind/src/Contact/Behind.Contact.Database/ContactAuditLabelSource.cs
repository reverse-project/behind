using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Microsoft.EntityFrameworkCore;

namespace Behind.Contact.Database;

public sealed class ContactAuditLabelSource(IContactContext context) : IAuditLabelSource
{
    public async Task<IReadOnlyList<AuditLabel>> LabelsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        using var scope = context.SoftDelete.IncludeDeleted();
        var forms = await context.ContactForms.OfKeys(ids).ToKeyedAsync(cancellationToken);
        var requests = await context.ContactRequests.OfKeys(ids).ToKeyedAsync(cancellationToken);
        return forms.Latest().Select(x => new AuditLabel(x.Key, x.Entity.Name))
            .Concat(requests.Latest().Select(x => new AuditLabel(x.Key, x.Entity.FormName)))
            .ToList();
    }
}
