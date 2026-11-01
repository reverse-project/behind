define(["ko", "models/about"], function (ko, about) {
    var scopes = [
        { key: "frontend", title: "Frontend libraries", description: "Bundled with the web interface and delivered to your browser." },
        { key: "server", title: "Server packages", description: "NuGet packages used by the Behind server, including transitive dependencies." },
        { key: "development", title: "Development and test tools", description: "Used to build, test and orchestrate Behind; not part of the running application." }
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
            title: scope.title,
            description: scope.description,
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
