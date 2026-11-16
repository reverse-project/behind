using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Microsoft.EntityFrameworkCore;

namespace Behind.Quotes.Database;

public sealed class QuotesAuditLabelSource(IQuotesContext context) : IAuditLabelSource
{
    private const int MaxLabelLength = 80;

    public async Task<IReadOnlyList<AuditLabel>> LabelsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        var rows = await context.Quotes
            .OfKeys(ids)
            .Select(quote => new
            {
                Key = EF.Property<Guid>(quote, VersionedColumns.Key),
                Version = EF.Property<long>(quote, VersionedColumns.Version),
                quote.Text
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.Key)
            .Select(versions => versions.MaxBy(row => row.Version)!)
            .Select(row => new AuditLabel(row.Key, row.Text.Shortened(MaxLabelLength)))
            .ToList();
    }
}
