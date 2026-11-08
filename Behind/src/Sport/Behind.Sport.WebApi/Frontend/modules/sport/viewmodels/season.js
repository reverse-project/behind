define(["ko", "modules/sport/models/sport", "modules/sport/models/detail", "modules/sport/models/format", "modules/sport/models/links"], function (ko, sport, createDetail, format, links) {
    return function SeasonViewModel(context) {
        var self = this;
        var seasonId = context.params.id;

        self.links = links;
        self.format = format;
        self.seasonId = seasonId;
        self.season = ko.observable(null);
        self.teams = ko.observableArray([]);
        self.name = ko.pureComputed(function () {
            return self.season() ? self.season().name : "";
        });

        var detail = createDetail(
            function () {
                return sport.season(seasonId);
            },
            function (data) {
                self.season(data.season);
                self.teams(data.teams);
            }
        );

        self.loading = detail.loading;
        self.message = detail.message;
        self.found = detail.found;
        self.missing = detail.missing;
        self.activate = detail.activate;
    };
});
