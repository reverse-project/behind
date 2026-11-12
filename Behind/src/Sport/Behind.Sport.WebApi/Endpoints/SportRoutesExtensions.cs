using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Sport.Common;
using Behind.Sport.Database;
using Behind.Sport.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Sport.WebApi.Endpoints;

public static class SportRoutesExtensions
{
    private const string Root = "/sport";

    extension(IEndpointRouteBuilder application)
    {
        public void MapSportEndpoints()
        {
            application.MapSportImages();
            application.MapResource<DisciplineDraft, DisciplineListing>("disciplines");
            application.MapResource<PositionDraft, PositionListing>("positions");
            application.MapResource<StatTypeDraft, StatTypeListing>("stat-types");
            application.MapResource<SeasonDraft, SeasonListing>("seasons");
            application.MapResource<TeamDraft, TeamListing>("teams");
            application.MapPlayers();
            application.MapResource<PlayerSeasonDraft, PlayerSeasonListing>("player-seasons");
            application.MapResource<StatDraft, StatListing>("stats");
            application.MapDetails();
        }

        private void MapResource<TDraft, TListing>(string name)
            where TDraft : class, IDraft<TDraft>
        {
            var path = $"{Root}/{name}";

            application.MapGet(path, ResourceEndpoints.List<TListing>)
                .Produces<SportListResponse<TListing>>()
                .Produces(400)
                .Produces(500);

            application.MapResourceActions<TDraft, TListing>(path);
        }

        private void MapPlayers()
        {
            var path = $"{Root}/players";

            application.MapGet(path, ResourceEndpoints.ListPlayers)
                .Produces<SportListResponse<PlayerPublicListing>>()
                .Produces(400)
                .Produces(500);

            application.MapResourceActions<PlayerDraft, PlayerListing>(path);
        }

        private void MapResourceActions<TDraft, TListing>(string path)
            where TDraft : class, IDraft<TDraft>
        {
            application.MapGet($"{path}/manage", ResourceEndpoints.Manage<TListing>)
                .RequireAuthorization(SportModuleNames.ReadGrant)
                .Produces<SportListResponse<TListing>>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost(path, ResourceEndpoints.Create<TDraft>)
                .RequireAuthorization(SportModuleNames.WriteGrant)
                .AddEndpointFilter<DataAnnotationValidationFilter<TDraft>>()
                .Produces<VersionedSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<VersionedConflictResponse>(409)
                .Produces(500);

            application.MapPost($"{path}/{{id:guid}}", ResourceEndpoints.Update<TDraft>)
                .RequireAuthorization(SportModuleNames.WriteGrant)
                .AddEndpointFilter<DataAnnotationValidationFilter<TDraft>>()
                .Produces<VersionedSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<VersionedConflictResponse>(409)
                .Produces(500);

            application.MapPost($"{path}/{{id:guid}}/delete", ResourceEndpoints.Delete<TDraft>)
                .RequireAuthorization(SportModuleNames.DeleteGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost($"{path}/{{id:guid}}/restore", ResourceEndpoints.Restore<TDraft>)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost($"{path}/{{id:guid}}/hard/delete", ResourceEndpoints.HardDelete<TDraft>)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);
        }

        private void MapSportImages()
        {
            application.MapGet($"{Root}/teams/images/{{id:guid}}", SportImageEndpoints.GetTeamLogo)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapGet($"{Root}/players/images/{{id:guid}}", SportImageEndpoints.GetPlayerPicture)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapPost($"{Root}/teams/images", SportImageEndpoints.UploadTeamLogo)
                .RequireAuthorization(SportModuleNames.WriteGrant)
                .DisableAntiforgery()
                .Produces<ImageUploadResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<ImageLimitResponse>(413)
                .Produces(415)
                .Produces(500);

            application.MapPost($"{Root}/players/images", SportImageEndpoints.UploadPlayerPicture)
                .RequireAuthorization(SportModuleNames.WriteGrant)
                .DisableAntiforgery()
                .Produces<ImageUploadResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<ImageLimitResponse>(413)
                .Produces(415)
                .Produces(500);
        }

        private void MapDetails()
        {
            application.MapGet($"{Root}/disciplines/{{id:guid}}", DetailEndpoints.Discipline)
                .Produces<DisciplineDetail>()
                .Produces(404)
                .Produces(500);

            application.MapGet($"{Root}/teams/{{id:guid}}", DetailEndpoints.Team)
                .Produces<TeamDetail>()
                .Produces(404)
                .Produces(500);

            application.MapGet($"{Root}/players/{{id:guid}}", DetailEndpoints.Player)
                .Produces<PlayerDetail>()
                .Produces(404)
                .Produces(500);

            application.MapGet($"{Root}/seasons/{{id:guid}}", DetailEndpoints.Season)
                .Produces<SeasonDetail>()
                .Produces(404)
                .Produces(500);

            application.MapGet($"{Root}/leaderboards/{{seasonId:guid}}", DetailEndpoints.Leaderboards)
                .Produces<SeasonLeaderboards>()
                .Produces(404)
                .Produces(500);

            application.MapGet($"{Root}/leaderboards/{{seasonId:guid}}/{{statTypeId:guid}}", DetailEndpoints.Leaderboard)
                .Produces<Leaderboard>()
                .Produces(404)
                .Produces(500);
        }
    }
}
