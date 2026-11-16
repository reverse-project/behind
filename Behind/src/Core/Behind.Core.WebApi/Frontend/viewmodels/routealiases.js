define(["ko", "models/session", "models/routealiases", "models/managedlist", "models/i18n", "models/contentlocale", "models/http"], function (ko, session, routeAliases, createManagedList, i18n, contentLocale, http) {
    return function RouteAliasesViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("RouteAlias.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("RouteAlias.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("RouteAlias.Delete");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        self.editingId = ko.observable(null);
        self.alias = ko.observable("");
        self.target = ko.observable("");
        self.localeLoadFailed = contentLocale.loadFailed;
        self.contentLocale = ko.observable("");
        self.localeLoaded = ko.observable(true);
        self.localeOptions = ko.pureComputed(function () {
            return [{ code: "", name: i18n.t("contentLocale.neutral") }].concat(contentLocale.locales());
        });

        self.isEditing = ko.pureComputed(function () {
            return self.editingId() !== null;
        });

        self.heading = ko.pureComputed(function () {
            return self.isEditing() ? i18n.t("routeAliases.editor.edit") : i18n.t("routeAliases.editor.new");
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !contentLocale.loadFailed() && self.localeLoaded() && !self.busy() && self.alias().trim() !== "" && self.target().trim() !== "";
        });

        var present = function (item) {
            var row = {
                id: item.id,
                alias: item.alias,
                target: item.target,
                href: "#/" + item.alias,
                isDeleted: item.isDeleted
            };

            row.edit = function () {
                self.editingId(row.id);
                self.alias(row.alias);
                self.target(row.target);
                self.localeLoaded(false);
                return http.get("/content-locales/RouteAlias/" + encodeURIComponent(row.id)).then(function (response) {
                    if (self.editingId() !== row.id) {
                        return;
                    }
                    if (response.ok) {
                        self.contentLocale(response.data.locale || "");
                        self.localeLoaded(true);
                    } else {
                        self.list.message(i18n.t("contentLocale.error"));
                    }
                }).catch(function () {
                    self.list.message(i18n.t("contentLocale.error"));
                });
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("routeAliases.confirm.delete", [row.alias]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return routeAliases.remove(row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return routeAliases.restore(row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("routeAliases.confirm.hardDelete", [row.alias]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return routeAliases.hardDelete(row.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: routeAliases.manage,
            pageOf: function (data) {
                return data.aliases;
            },
            present: present
        });

        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            self.alias("");
            self.target("");
            self.contentLocale("");
            self.localeLoaded(true);
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = { alias: self.alias().trim(), target: self.target().trim(), contentLocale: self.contentLocale() || null };

            return self.list.perform(function () {
                var saving = self.isEditing() ? routeAliases.save(self.editingId(), fields) : routeAliases.create(fields);

                return saving.then(function (result) {
                    if (result.ok) {
                        self.reset();
                    }

                    return result;
                });
            });
        };

        self.activate = self.list.activate;
    };
});
