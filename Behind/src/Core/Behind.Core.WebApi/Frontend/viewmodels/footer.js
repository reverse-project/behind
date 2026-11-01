define(["ko", "models/about"], function (ko, about) {
    return function FooterViewModel() {
        var self = this;

        self.name = "Behind";
        self.version = ko.observable("");

        self.hasVersion = ko.pureComputed(function () {
            return self.version() !== "";
        });

        self.label = ko.pureComputed(function () {
            return "v" + self.version();
        });

        self.activate = function () {
            return about.loadVersion().then(self.version);
        };
    };
});
