define(["ko", "models/about", "models/i18n"], function (ko, about, i18n) {
    var scopes = [
        { key: "frontend", title: "licenses.scopes.frontend.title", description: "licenses.scopes.frontend.description" },
        { key: "server", title: "licenses.scopes.server.title", description: "licenses.scopes.server.description" },
        { key: "development", title: "licenses.scopes.development.title", description: "licenses.scopes.development.description" }
    ];

    var byName = function (left, right) {
        return left.name.toLowerCase().localeCompare(right.name.toLowerCase());
    };

    var present = function (license) {
        return {
            name: license.name,
            version: license.version,
            license: license.license,
            copyright: license.copyright,
            url: license.url,
            text: "/" + license.text,
            note: license.note || "",
            hasNote: !!license.note
        };
    };

    var presentScope = function (licenses, scope) {
        return {
            title: ko.pureComputed(function () {
                return i18n.t(scope.title);
            }),
            description: ko.pureComputed(function () {
                return i18n.t(scope.description);
            }),
            licenses: licenses
                .filter(function (license) {
                    return license.scope === scope.key;
                })
                .sort(byName)
                .map(present)
        };
    };

    return function LicensesViewModel() {
        var self = this;

        self.name = "Behind";
        self.license = "GPL-2.0-or-later";
        self.copyright = "Copyright (c) 2026 Behind contributors";
        self.text = "/licenses/Behind.txt";

        self.busy = ko.observable(false);
        self.failed = ko.observable(false);
        self.scopes = ko.observableArray([]);

        self.activate = function () {
            self.busy(true);

            return about.loadLicenses()
                .then(function (result) {
                    self.failed(!result.ok);
                    self.scopes(scopes
                        .map(function (scope) {
                            return presentScope(result.licenses, scope);
                        })
                        .filter(function (scope) {
                            return scope.licenses.length > 0;
                        }));
                })
                .finally(function () {
                    self.busy(false);
                });
        };
    };
});
