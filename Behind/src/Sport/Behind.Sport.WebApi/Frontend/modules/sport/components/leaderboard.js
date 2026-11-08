define(["ko", "text!modules/sport/components/leaderboard.html", "modules/sport/models/format"], function (ko, template, format) {
    var present = function (entry) {
        var isTeam = entry.scope === "Team";

        return {
            rank: entry.rank,
            kind: isTeam ? "team" : "player",
            subjectId: entry.subjectId,
            subjectName: entry.subjectName,
            image: isTeam ? entry.logoUrl : entry.pictureUrl,
            teamId: isTeam ? null : entry.teamId,
            teamName: isTeam ? "" : entry.teamName,
            value: entry.value
        };
    };

    return {
        viewModel: {
            createViewModel: function (params) {
                return {
                    format: format,
                    rows: ko.pureComputed(function () {
                        return (ko.unwrap(params.entries) || []).map(present);
                    })
                };
            }
        },
        template: template
    };
});
