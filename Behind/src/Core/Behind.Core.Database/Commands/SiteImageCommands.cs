using Behind.Core.Domain;

namespace Behind.Core.Database.Commands;

public sealed class SiteImageCommands(AbstractCoreContext context) : ISiteImageCommands
{
    public async Task<Guid> Add(SiteImage image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        await context.SiteImages.AddAsync(image, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return context.GetKey(image);
    }
}
