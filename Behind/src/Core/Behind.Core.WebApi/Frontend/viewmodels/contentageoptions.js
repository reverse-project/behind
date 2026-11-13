define(["ko", "models/http", "models/i18n", "models/session"], function (ko, http, i18n, session) {
    return function ContentAgeOptionsViewModel() {
        var self = this;
        var disposed = false;
        var request = 0;
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("ContentAge.Manage");
        });
        self.canViewDeleted = ko.pureComputed(function () {
            return self.canManage() && session.hasGrant("RawDatabase.Manage");
        });
        self.items = ko.observableArray([]);
        self.age = ko.observable("");
        self.name = ko.observable("");
        self.editingId = ko.observable(null);
        self.showDeleted = ko.observable(false);
        self.loading = ko.observable(false);
        self.saving = ko.observable(false);
        self.message = ko.observable("");
        self.busy = ko.pureComputed(function () {
            return self.loading() || self.saving();
        });
        var validAge = function () {
            var value = self.age();
            var age = Number(value);
            return (typeof value === "number" || typeof value === "string")
                && Number.isInteger(age) && age >= 1 && age <= 150;
        };
        var validName = function () {
            return typeof self.name() === "string" && self.name().trim().length > 0
                && self.name().trim().length <= 200;
        };
        self.canSave = ko.pureComputed(function () {
            return self.canManage() && !self.busy() && !self.showDeleted() && validAge() && validName();
        });
        self.cancelEdit = function () {
            self.editingId(null);
            self.age("");
            self.name("");
            self.message("");
        };
        self.edit = function (item) {
            if (!self.canManage() || self.busy() || self.showDeleted() || self.items().indexOf(item) < 0) {
                return;
            }
            self.editingId(item.id);
            self.age(item.age);
            self.name(item.name);
            self.message("");
        };
        var load = function () {
            var current = ++request;
            self.items([]);
            self.message("");
            if (disposed || !self.canManage() || (self.showDeleted() && !self.canViewDeleted())) {
                self.loading(false);
                return Promise.resolve();
            }
            self.loading(true);
            return http.get("/content-age-options?deleted=" + self.showDeleted()).then(function (response) {
                if (disposed || current !== request || !self.canManage()) {
                    return;
                }
                if (response.ok) {
                    self.items(response.data.items);
                } else {
                    self.message(i18n.t("contentAgeOptions.failed"));
                }
            }).catch(function () {
                if (!disposed && current === request && self.canManage()) {
                    self.message(i18n.t("contentAgeOptions.failed"));
                }
            }).finally(function () {
                if (!disposed && current === request) {
                    self.loading(false);
                }
            });
        };
        var perform = function (url, body, clearForm) {
            self.saving(true);
            self.message("");
            return http.post(url, body).then(function (response) {
                if (disposed || !self.canManage()) {
                    return;
                }
                if (!response.ok) {
                    self.message(i18n.t("contentAgeOptions.failed"));
                    return;
                }
                if (clearForm) {
                    self.cancelEdit();
                }
                return load();
            }).catch(function () {
                if (!disposed && self.canManage()) {
                    self.message(i18n.t("contentAgeOptions.failed"));
                }
            }).finally(function () {
                if (!disposed) {
                    self.saving(false);
                }
            });
        };
        self.save = function () {
            if (disposed || !self.canManage() || self.busy() || self.showDeleted()) {
                return Promise.resolve();
            }
            if (!validAge()) {
                self.message(i18n.t("contentAgeOptions.invalidAge"));
                return Promise.resolve();
            }
            if (!validName()) {
                self.message(i18n.t("contentAgeOptions.invalidName"));
                return Promise.resolve();
            }
            var id = self.editingId();
            return id
                ? perform("/content-age-options/" + encodeURIComponent(id) + "/update", { name: self.name().trim() }, true)
                : perform("/content-age-options", { age: Number(self.age()), name: self.name().trim() }, true);
        };
        var change = function (item, action, deleted, confirmation) {
            if (disposed || !self.canManage() || (deleted && !self.canViewDeleted()) || self.busy() || self.showDeleted() !== deleted || self.items().indexOf(item) < 0) {
                return Promise.resolve();
            }
            if (confirmation && !window.confirm(i18n.t(confirmation, [item.age]))) {
                return Promise.resolve();
            }
            return perform("/content-age-options/" + encodeURIComponent(item.id) + "/" + action);
        };
        self.remove = function (item) {
            return change(item, "delete", false, "contentAgeOptions.confirm.delete");
        };
        self.restore = function (item) {
            return change(item, "restore", true);
        };
        self.hardDelete = function (item) {
            return change(item, "hard/delete", true, "contentAgeOptions.confirm.hardDelete");
        };
        var deletedSubscription = self.showDeleted.subscribe(function () {
            self.cancelEdit();
            load();
        });
        var permissionSubscription = self.canManage.subscribe(function () {
            self.cancelEdit();
            load();
        });
        var deletedPermissionSubscription = self.canViewDeleted.subscribe(function (allowed) {
            if (!allowed && self.showDeleted()) {
                self.showDeleted(false);
            }
        });
        self.activate = load;
        self.dispose = function () {
            disposed = true;
            ++request;
            deletedSubscription.dispose();
            permissionSubscription.dispose();
            deletedPermissionSubscription.dispose();
            self.canSave.dispose();
            self.busy.dispose();
            self.canManage.dispose();
            self.canViewDeleted.dispose();
        };
    };
});
