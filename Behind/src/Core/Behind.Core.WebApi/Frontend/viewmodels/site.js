define(["ko", "models/sites"], function (ko, sites) {
    return function SiteViewModel(context) {
        var self = this;

        self.loading = ko.observable(true);
        self.title = ko.observable("");
        self.content = ko.observable("");
        self.message = ko.observable("");

        self.found = ko.pureComputed(function () {
            return !self.loading() && self.message() === "";
        });

        self.missing = ko.pureComputed(function () {
            return !self.loading() && self.message() !== "";
        });

        self.activate = function () {
            var loading = context.loaded ? Promise.resolve(context.loaded) : sites.loadPublic(context.params.slug);

            return loading
                .then(function (result) {
                    self.message(result.message);

                    if (result.ok) {
                        self.title(result.data.title);
                        self.content(result.data.content);
                    }
                })
                .finally(function () {
                    self.loading(false);
                });
        };
    };
});
