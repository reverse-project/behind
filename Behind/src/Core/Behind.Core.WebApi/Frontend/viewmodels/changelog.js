define(["ko", "models/about", "models/i18n"], function (ko, about, i18n) {
    var typeLabels = {
        added: "changelog.types.added",
        changed: "changelog.types.changed",
        fixed: "changelog.types.fixed",
        security: "changelog.types.security"
    };

    var present = function (change) {
        return {
            type: ko.pureComputed(function () {
                return i18n.tOr(typeLabels[change.type], change.type);
            }),
            text: change.text
        };
    };

    var presentRelease = function (release) {
        return {
            version: release.version,
            date: ko.pureComputed(function () {
                return i18n.date(release.date);
            }),
            changes: (release.changes || []).map(present)
        };
    };

    return function ChangelogViewModel() {
        var self = this;

        self.busy = ko.observable(false);
        self.failed = ko.observable(false);
        self.releases = ko.observableArray([]);

        self.isEmpty = ko.pureComputed(function () {
            return self.releases().length === 0 && !self.busy() && !self.failed();
        });

        self.activate = function () {
            self.busy(true);

            return about.loadChangelog()
                .then(function (result) {
                    self.failed(!result.ok);
                    self.releases(result.releases.map(presentRelease));
                })
                .finally(function () {
                    self.busy(false);
                });
        };
    };
});
