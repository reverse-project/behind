define(["ko", "modules/sport/models/sport", "modules/sport/models/detail", "modules/sport/models/format", "modules/sport/models/links"], function (ko, sport, createDetail, format, links) {
    return function PlayerViewModel(context) {
        var self = this;
        var playerId = context.params.id;

        self.links = links;
        self.format = format;
        self.player = ko.observable(null);
        self.seasons = ko.observableArray([]);
        self.name = ko.pureComputed(function () {
            return self.player() ? self.player().name : "";
        });
        self.biography = ko.pureComputed(function () {
            return self.player() ? format.lines(self.player().biography) : [];
        });

        var detail = createDetail(
            function () {
                return sport.player(playerId);
            },
            function (data) {
                self.player(data.player);
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
