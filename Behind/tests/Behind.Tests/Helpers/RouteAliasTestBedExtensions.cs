using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.Helpers;

public static class RouteAliasTestBedExtensions
{
    public static IRouteAliasCommands.RouteAliasDraft Draft(
        string alias = "manuals",
        string target = "#/downloads/products/manuals") =>
        new(alias, target);

    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithRouteAliasCommandsAsync<TResult>(
            Func<RouteAliasCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new RouteAliasCommands(ctx));
        }

        public async Task<TResult> WithRouteAliasQueriesAsync<TResult>(
            Func<RouteAliasQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new RouteAliasQueries(ctx));
        }

        public async Task<Guid> CreateRouteAliasAsync(
            string alias = "manuals",
            string target = "#/downloads/products/manuals")
        {
            var result = await bed.WithRouteAliasCommandsAsync(commands => commands.Create(Draft(alias, target)));

            return Assert.IsType<RouteAliasSaveResult.Saved>(result).Id.Key;
        }
    }
}
