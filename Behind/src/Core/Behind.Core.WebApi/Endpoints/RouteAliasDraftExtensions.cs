using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class RouteAliasDraftExtensions
{
    extension(RouteAliasEndpoints.SaveRouteAliasRequest request)
    {
        public Option<IRouteAliasCommands.RouteAliasDraft> ToDraft()
        {
            var target = (request.Target ?? string.Empty).Trim();

            return (request.Alias ?? string.Empty).ToSlug(RouteAlias.MaxAliasLength).Bind(alias =>
                target.IsValidRouteTarget
                    ? Some(new IRouteAliasCommands.RouteAliasDraft(alias, target))
                    : None);
        }
    }
}
