using Behind.Common;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class SiteImageQueries(AbstractCoreContext context) : ISiteImageQueries
{
    public async Task<Option<SiteImage>> Find(Guid id, CancellationToken cancellationToken = default)
    {
        var images = await context.SiteImages
            .Where(image => EF.Property<Guid>(image, "Id") == id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return images.Head();
    }
}
