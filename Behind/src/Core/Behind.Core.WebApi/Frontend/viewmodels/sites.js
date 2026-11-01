define(["ko", "models/session", "models/sites", "models/managedlist"], function (ko, session, sites, createManagedList) {
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

    return function SitesViewModel() {
        var self = this;
        var indexSlug = "";

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Site.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Site.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Site.Delete");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        var present = function (item) {
            var status = statusOf(item);
            var row = {
                id: item.id,
                title: item.title,
                slug: item.slug,
                latestVersion: item.latestVersion,
                activeVersion: item.activeVersion === null ? "none" : item.activeVersion,
                updatedAt: formatDate(item.updatedAt),
                status: status.label,
                statusCss: status.css,
                isDeleted: item.isDeleted,
                isIndex: indexSlug !== "" && item.activeSlug === indexSlug,
                href: "#/sites/" + item.id
            };

            row.remove = function () {
                if (!window.confirm("Delete " + row.title + "? It stops being served and can be restored by a developer.")) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return sites.remove(item.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return sites.restore(item.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm("Permanently delete " + row.title + " with all its versions? This cannot be undone.")) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return sites.hardDelete(item.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: function (query) {
                return Promise.all([sites.list(query), sites.settings()]).then(function (results) {
                    indexSlug = results[1].ok ? results[1].data.indexSlug || "" : "";

                    return results[0];
                });
            },
            pageOf: function (data) {
                return data.sites;
            },
            present: present
        });

        self.activate = self.list.activate;
    };
});
