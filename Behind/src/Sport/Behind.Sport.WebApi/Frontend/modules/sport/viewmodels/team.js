define(["ko", "modules/sport/models/sport", "modules/sport/models/detail", "modules/sport/models/format", "modules/sport/models/links"], function (ko, sport, createDetail, format, links) {
    return function TeamViewModel(context) {
        var self = this;
        var teamId = context.params.id;

        self.links = links;
        self.format = format;
        self.team = ko.observable(null);
        self.seasons = ko.observableArray([]);
        self.name = ko.pureComputed(function () {
            return self.team() ? self.team().name : "";
        });
        self.description = ko.pureComputed(function () {
            return self.team() ? format.lines(self.team().description) : [];
        });
        self.origin = ko.pureComputed(function () {
            return self.team() ? [self.team().city, self.team().country].filter(Boolean).join(", ") : "";
        });

        var detail = createDetail(
            function () {
                return sport.team(teamId);
            },
            function (data) {
                self.team(data.team);
                self.seasons(data.seasons);
            }
        );

        self.loading = detail.loading;
        self.message = detail.message;
        self.found = detail.found;
        self.missing = detail.missing;
        self.activate = detail.activate;
    };
});
