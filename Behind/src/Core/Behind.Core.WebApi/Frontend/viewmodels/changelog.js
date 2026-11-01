define(["ko", "models/about"], function (ko, about) {
    var typeLabels = {
        added: "Added",
        changed: "Changed",
        fixed: "Fixed",
        security: "Security"
    };

    var present = function (change) {
        return {
            type: typeLabels[change.type] || change.type,
            text: change.text
        };
    };

    var formatDate = function (value) {
        return value ? new Date(value).toLocaleDateString() : "";
    };

    var presentRelease = function (release) {
        return {
            version: release.version,
            date: formatDate(release.date),
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
