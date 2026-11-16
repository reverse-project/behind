define(["ko", "modules/sport/models/sport", "modules/sport/models/detail", "modules/sport/models/format", "modules/sport/models/links"], function (ko, sport, createDetail, format, links) {
    return function DisciplineViewModel(context) {
        var self = this;

        self.links = links;
        self.discipline = ko.observable(null);
        self.positions = ko.observableArray([]);
        self.statTypes = ko.observableArray([]);
        self.seasons = ko.observableArray([]);
        self.name = ko.pureComputed(function () {
            return self.discipline() ? self.discipline().name : "";
        });
        self.disciplineId = context.params.id;
        self.newestSeasonId = ko.pureComputed(function () {
            return self.seasons().length > 0 ? self.seasons()[0].id : null;
        });

        var detail = createDetail(
            function () {
                return sport.discipline(self.disciplineId);
            },
            function (data) {
                self.discipline(data.discipline);
                self.positions(data.positions);
                self.statTypes(data.statTypes);
                self.seasons(format.newestFirst(data.seasons));
            }
        );

        self.loading = detail.loading;
        self.message = detail.message;
        self.found = detail.found;
        self.missing = detail.missing;
        self.activate = detail.activate;
    };
});
