define(["ko", "models/session", "modules/sport/models/sport", "modules/sport/models/links"], function (ko, session, sport, links) {
    return function SportViewModel() {
        var self = this;

        self.links = links;
        self.message = ko.observable("");
        self.disciplines = ko.observableArray([]);
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("Sport.Write");
        });
        self.hasDisciplines = ko.pureComputed(function () {
            return self.disciplines().length > 0;
        });

        self.activate = function () {
            return sport.list("disciplines", { pageSize: 100 }).then(function (result) {
                self.message(result.message);
                self.disciplines(result.ok ? result.data.page.items : []);
            });
        };
    };
});
