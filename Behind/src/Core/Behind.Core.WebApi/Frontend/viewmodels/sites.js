define(["ko", "models/session", "models/sites"], function (ko, session, sites) {
    var formatDate = function (value) {
        return value ? new Date(value).toLocaleString() : "";
    };

    var statusOf = function (item) {
        if (item.activeVersion === null) {
            return { label: "Draft", css: "text-bg-secondary" };
        }

        return item.latestVersion > item.activeVersion
            ? { label: "Live with newer draft", css: "text-bg-warning" }
            : { label: "Live", css: "text-bg-success" };
    };

    var present = function (item, indexSlug) {
        var status = statusOf(item);

        return {
            id: item.id,
            title: item.title,
            slug: item.slug,
            latestVersion: item.latestVersion,
            activeVersion: item.activeVersion === null ? "none" : item.activeVersion,
            updatedAt: formatDate(item.updatedAt),
            status: status.label,
            statusCss: status.css,
            isIndex: indexSlug !== "" && item.activeSlug === indexSlug,
            href: "#/sites/" + item.id
        };
    };

    return function SitesViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Site.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Site.Write");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.sites = ko.observableArray([]);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.hasSites = ko.pureComputed(function () {
            return self.sites().length > 0;
        });

        self.refresh = function () {
            if (!self.canRead()) {
                self.sites([]);
                return Promise.resolve();
            }

            self.busy(true);

            return Promise.all([sites.list(), sites.settings()])
                .then(function (results) {
                    var result = results[0];
                    var indexSlug = results[1].ok ? results[1].data.indexSlug || "" : "";

                    self.message(result.message);
                    self.sites(result.ok ? result.data.sites.map(function (item) {
                        return present(item, indexSlug);
                    }) : []);
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.activate = function () {
            self.canRead.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                }
            });

            return self.refresh();
        };
    };
});
