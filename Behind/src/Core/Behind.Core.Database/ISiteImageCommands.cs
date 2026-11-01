using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISiteImageCommands
{
    Task<Guid> Add(SiteImage image, CancellationToken cancellationToken = default);
}
