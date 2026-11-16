define(["ko", "modules/sport/models/sport", "modules/sport/models/listing", "modules/sport/models/links"], function (ko, sport, createListing, links) {
    return function TeamsViewModel(context) {
        var self = this;
        var disciplineId = context.params.discipline || null;

        self.links = links;
        self.disciplineId = disciplineId;
        self.disciplines = ko.observableArray([]);
        self.list = createListing({
            resource: "teams",
            pageSize: 12,
            filters: function () {
                return { discipline: disciplineId };
            }
        });

        self.activate = function () {
            return Promise.all([
                sport.list("disciplines", { pageSize: 100 }).then(function (result) {
                    self.disciplines(result.ok ? result.data.page.items : []);
                }),
                self.list.refresh()
            ]);
        };
    };
});
