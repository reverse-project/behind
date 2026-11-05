using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IRouteAliasCommands
{
    public sealed record RouteAliasDraft(string Alias, string Target);

    Task<RouteAliasSaveResult> Create(RouteAliasDraft draft, CancellationToken cancellationToken = default);

    Task<RouteAliasSaveResult> Update(
        Guid aliasId,
        RouteAliasDraft draft,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid aliasId, CancellationToken cancellationToken = default);

    Task<RouteAliasSaveResult> Restore(Guid aliasId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid aliasId, CancellationToken cancellationToken = default);
}
