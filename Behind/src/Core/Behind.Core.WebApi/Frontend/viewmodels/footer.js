define(["ko", "models/about", "models/branding"], function (ko, about, branding) {
    return function FooterViewModel() {
        var self = this;

        self.name = "Behind";
        self.version = ko.observable("");
        self.copyright = branding.copyright;

        self.hasCopyright = ko.pureComputed(function () {
            return self.copyright() !== "";
        });

        self.hasVersion = ko.pureComputed(function () {
            return self.version() !== "";
        });

        self.label = ko.pureComputed(function () {
            return "v" + self.version();
        });

        self.scrollToTop = function () {
            window.scrollTo({ top: 0, behavior: "smooth" });
        };

        self.activate = function () {
            return about.loadVersion().then(self.version);
        };
    };
});
