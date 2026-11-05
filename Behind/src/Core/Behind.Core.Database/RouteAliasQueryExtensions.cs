using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal sealed record RouteAliasRow(Guid Key, long Version, string Alias, string Target);

internal static class RouteAliasQueryExtensions
{
    extension(IQueryable<RouteAlias> aliases)
    {
        public IQueryable<RouteAliasRow> ToRows() =>
            aliases
                .OrderBy(alias => alias.Alias)
                .ThenBy(alias => EF.Property<Guid>(alias, VersionedColumns.Key))
                .Select(alias => new RouteAliasRow(
                    EF.Property<Guid>(alias, VersionedColumns.Key),
                    EF.Property<long>(alias, VersionedColumns.Version),
                    alias.Alias,
                    alias.Target));
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<RouteAlias> LatestRouteAliasesIn(DeletionState state) =>
            context.RouteAliases
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.RouteAliases);
    }
}
