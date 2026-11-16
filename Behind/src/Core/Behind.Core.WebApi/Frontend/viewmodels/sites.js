define(["ko", "models/session", "models/sites", "models/managedlist", "models/i18n"], function (ko, session, sites, createManagedList, i18n) {
    var statusOf = function (item) {
        if (item.activeVersion === null) {
            return { key: "sites.status.draft", css: "text-bg-secondary" };
        }

        return item.latestVersion > item.activeVersion
            ? { key: "sites.status.liveNewerDraft", css: "text-bg-warning" }
            : { key: "sites.status.live", css: "text-bg-success" };
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
                activeVersion: ko.pureComputed(function () {
                    return item.activeVersion === null ? i18n.t("sites.noneLive") : item.activeVersion;
                }),
                updatedAt: ko.pureComputed(function () {
                    return i18n.dateTime(item.updatedAt);
                }),
                status: ko.pureComputed(function () {
                    return i18n.t(status.key);
                }),
                statusCss: status.css,
                isDeleted: item.isDeleted,
                isIndex: indexSlug !== "" && item.activeSlug === indexSlug,
                href: "#/sites/" + item.id
            };

            row.remove = function () {
                if (!window.confirm(i18n.t("sites.confirm.delete", [row.title]))) {
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
                if (!window.confirm(i18n.t("sites.confirm.hardDelete", [row.title]))) {
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
