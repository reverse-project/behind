define(["ko", "text!modules/sport/components/stats.html", "modules/sport/models/format"], function (ko, template, format) {
    var present = function (stat) {
        return {
            seasonId: stat.seasonId,
            seasonName: stat.seasonName,
            statTypeId: stat.statTypeId,
            statTypeName: stat.statTypeName,
            abbreviation: stat.statTypeAbbreviation,
            subjectKind: stat.scope === "Team" ? "team" : "player",
            subjectId: stat.subjectId,
            subjectName: stat.subjectName,
            value: stat.value
        };
    };

    return {
        viewModel: {
            createViewModel: function (params) {
                return {
                    format: format,
                    showSeason: !!ko.unwrap(params.showSeason),
                    showSubject: !!ko.unwrap(params.showSubject),
                    rows: ko.pureComputed(function () {
                        return (ko.unwrap(params.stats) || []).map(present);
                    })
                };
            }
        },
        template: template
    };
});
