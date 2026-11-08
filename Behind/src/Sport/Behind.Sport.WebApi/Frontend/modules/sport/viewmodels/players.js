define(["ko", "modules/sport/models/sport", "modules/sport/models/listing", "modules/sport/models/format", "modules/sport/models/links"], function (ko, sport, createListing, format, links) {
    return function PlayersViewModel(context) {
        var self = this;
        var positionId = context.params.position || null;
        var disciplineId = ko.observable(context.params.discipline || null);

        self.links = links;
        self.format = format;
        self.positionId = positionId;
        self.disciplineId = disciplineId;
        self.disciplines = ko.observableArray([]);
        self.positions = ko.observableArray([]);
        self.positionName = ko.observable("");

        var loadPositions = function () {
            return sport.list("positions", { discipline: disciplineId(), pageSize: 100 }).then(function (result) {
                self.positions(result.ok ? result.data.page.items : []);
            });
        };

        var learnFrom = function (items) {
            if (positionId === null || items.length === 0 || disciplineId() !== null) {
                return;
            }

            self.positionName(items[0].positionName || "");
            disciplineId(items[0].disciplineId);
            loadPositions();
        };

        self.list = createListing({
            resource: "players",
            pageSize: 12,
            filters: function () {
                return { discipline: disciplineId(), position: positionId };
            },
            loaded: learnFrom
        });

        self.activate = function () {
            return Promise.all([
                sport.list("disciplines", { pageSize: 100 }).then(function (result) {
                    self.disciplines(result.ok ? result.data.page.items : []);
                }),
                disciplineId() === null ? Promise.resolve() : loadPositions(),
                self.list.refresh()
            ]);
        };
    };
});
