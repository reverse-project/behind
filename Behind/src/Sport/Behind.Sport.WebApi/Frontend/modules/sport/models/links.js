define(function () {
    var root = "#/sport";

    var entity = function (path) {
        return function (id) {
            return id ? root + "/" + path + "/" + id : null;
        };
    };

    var optionalSeason = function (path) {
        return function (season, statType) {
            return [root + "/" + path].concat(season ? [season] : [], season && statType ? [statType] : []).join("/");
        };
    };

    var discipline = entity("disciplines");
    var team = entity("teams");
    var player = entity("players");
    var season = entity("seasons");
    var position = entity("players/position");

    var statType = function (seasonId, id) {
        if (!id) {
            return null;
        }

        return seasonId ? root + "/leaderboards/" + seasonId + "/" + id : root + "/stats";
    };

    var of = function (kind, id, seasonId) {
        var builders = {
            discipline: discipline,
            team: team,
            player: player,
            season: season,
            position: position,
            statType: function (statTypeId) {
                return statType(seasonId, statTypeId);
            }
        };

        return builders[kind] ? builders[kind](id) : null;
    };

    return {
        home: root,
        teams: root + "/teams",
        players: root + "/players",
        seasons: root + "/seasons",
        manage: "#/manage/sport",
        discipline: discipline,
        team: team,
        player: player,
        season: season,
        position: position,
        statType: statType,
        of: of,
        teamsOf: function (disciplineId) {
            return disciplineId ? root + "/teams/discipline/" + disciplineId : root + "/teams";
        },
        playersOf: function (disciplineId) {
            return disciplineId ? root + "/players/discipline/" + disciplineId : root + "/players";
        },
        leaderboards: optionalSeason("leaderboards"),
        stats: optionalSeason("stats"),
        manageResource: function (resource) {
            return "#/manage/sport/" + resource;
        }
    };
});
