using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record RouteAliasTargetResponse(string Alias, string Target);

public sealed record ManagedRouteAlias(Guid Id, string Alias, string Target, bool IsDeleted);

public sealed record RouteAliasManageResponse(PageResponse<ManagedRouteAlias> Aliases, IReadOnlyList<string> Letters);
