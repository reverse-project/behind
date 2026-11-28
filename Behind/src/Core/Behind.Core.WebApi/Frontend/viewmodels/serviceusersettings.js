define(["ko", "models/settings", "models/i18n", "models/access", "models/modules"], function (ko, settings, i18n, access, modules) {
    return function ServiceUserSettings(canEdit) {
        var self = this;
        var generation = 0;
        var accessGeneration = 0;
        self.users = ko.observableArray([]);
        self.loaded = ko.observable(false);
        self.busy = ko.observable(false);
        self.deleted = ko.observable(false);
        self.canManage = ko.observable(false);
        self.canDelete = ko.observable(false);
        self.isDeveloper = ko.observable(false);
        self.name = ko.observable("");
        self.key = ko.observable("");
        self.message = ko.observable("");
        self.accessId = ko.observable(null);
        self.accessVersion = ko.observable(null);
        self.accessLoading = ko.observable(false);
        self.accessCanEdit = ko.observable(false);
        self.availableRoles = ko.observableArray([]);
        self.selectedRoleIds = ko.observableArray([]);
        self.accessEditor = access.create();
        self.calculatedGrants = ko.pureComputed(function () {
            var selected = self.availableRoles().filter(function (role) {
                return self.selectedRoleIds().indexOf(role.id) >= 0;
            });
            var selection = self.accessEditor.request();
            return modules.visibleGrants(access.calculate(selected, selection.grants, selection.restrictions));
        });

        self.canWrite = ko.pureComputed(function () {
            return canEdit() && self.loaded() && !self.busy() && self.canManage();
        });
        self.canRemove = ko.pureComputed(function () {
            return canEdit() && self.loaded() && !self.busy() && self.canDelete();
        });
        self.canDevelop = ko.pureComputed(function () {
            return canEdit() && self.loaded() && !self.busy() && self.isDeveloper();
        });
        self.canCreate = ko.pureComputed(function () {
            return self.canWrite() && !!self.name().trim();
        });
        self.canSaveAccess = ko.pureComputed(function () {
            return self.canWrite() && self.accessCanEdit() && !self.accessLoading();
        });
        var clearAccess = function () {
            accessGeneration++;
            self.accessId(null);
            self.accessVersion(null);
            self.accessLoading(false);
            self.accessCanEdit(false);
            self.availableRoles([]);
            self.selectedRoleIds([]);
            self.accessEditor.load([], [], []);
        };
        self.invalidate = function () {
            generation++;
            clearAccess();
            self.key("");
            self.users([]);
            self.loaded(false);
            self.busy(false);
            self.deleted(false);
            self.canManage(false);
            self.canDelete(false);
            self.isDeveloper(false);
            self.message("");
        };
        self.load = function () {
            var current = ++generation;
            clearAccess();
            self.key("");
            self.loaded(false);
            self.busy(true);
            return settings.loadServiceUsers(self.deleted()).then(function (result) {
                if (current !== generation) {
                    return;
                }
                self.busy(false);
                self.message(result.message || "");
                if (result.ok) {
                    self.users(result.data.users);
                    self.canManage(result.data.canManage);
                    self.canDelete(result.data.canDelete);
                    self.isDeveloper(result.data.isDeveloper);
                    self.loaded(true);
                }
            }).catch(function () {
                if (current === generation) {
                    self.busy(false);
                    self.message(i18n.t("settings.error.generic"));
                }
            });
        };
        self.showDeleted = function () {
            if (self.isDeveloper() && !self.busy()) {
                self.deleted(!self.deleted());
                return self.load();
            }
        };
        var act = function (action, user, request, allowed, confirmation) {
            if (!allowed() || (confirmation && !window.confirm(i18n.t(confirmation)))) {
                return Promise.resolve();
            }
            var current = ++generation;
            self.key("");
            self.busy(true);
            self.message("");
            return settings.serviceUserAction(action, user && user.id, request).then(function (result) {
                if (current !== generation) {
                    return;
                }
                self.busy(false);
                if (!result.ok) {
                    self.message(result.message);
                    return;
                }
                if (action === "create") {
                    self.name("");
                }
                var secret = result.data && result.data.apiKey;
                return self.load().then(function () {
                    if (generation === current + 1 && secret && canEdit()) {
                        self.key(secret);
                    }
                });
            }).catch(function () {
                if (current === generation) {
                    self.busy(false);
                    self.message(i18n.t("settings.error.generic"));
                }
            });
        };
        self.create = function () {
            return act("create", null, { name: self.name().trim() }, self.canCreate);
        };
        self.openAccess = function (user) {
            if (self.accessId() === user.id) {
                clearAccess();
                return Promise.resolve();
            }
            clearAccess();
            self.accessId(user.id);
            self.accessLoading(true);
            var current = accessGeneration;
            return settings.loadServiceUserAccess(user.id).then(function (result) {
                if (current !== accessGeneration) {
                    return;
                }
                self.accessLoading(false);
                self.message(result.message || "");
                if (result.ok) {
                    var data = result.data;
                    self.accessVersion(data.version);
                    self.accessCanEdit(data.canEdit);
                    self.availableRoles(data.availableRoles);
                    self.selectedRoleIds(data.roles.map(function (role) { return role.id; }));
                    self.accessEditor.load(data.availableGrants, data.grants, data.restrictions);
                }
            }).catch(function () {
                if (current === accessGeneration) {
                    self.accessLoading(false);
                    self.message(i18n.t("settings.error.generic"));
                }
            });
        };
        self.saveAccess = function () {
            if (!self.canSaveAccess() || !self.accessId()) {
                return Promise.resolve();
            }
            var id = self.accessId();
            var current = accessGeneration;
            var selection = self.accessEditor.request();
            self.accessLoading(true);
            return settings.saveServiceUserAccess(id, {
                version: self.accessVersion(),
                roleIds: self.selectedRoleIds().slice(),
                grants: selection.grants,
                restrictions: selection.restrictions
            }).then(function (result) {
                if (current === accessGeneration) {
                    self.accessLoading(false);
                    self.message(result.message || "");
                    if (result.ok) {
                        self.accessId(null);
                        return self.openAccess({ id: id });
                    }
                }
            }).catch(function () {
                if (current === accessGeneration) {
                    self.accessLoading(false);
                    self.message(i18n.t("settings.error.generic"));
                }
            });
        };
        self.dispose = function () {
            self.invalidate();
            self.accessEditor.dispose();
            self.calculatedGrants.dispose();
        };
        self.toggle = function (user) {
            return act("update", user, { version: user.version, enabled: !user.enabled }, self.canWrite);
        };
        self.rotate = function (user) {
            return act("rotate", user, { version: user.version }, self.canWrite, "settings.serviceUsers.confirmRotate");
        };
        self.remove = function (user) {
            return act("delete", user, { version: user.version }, self.canRemove, "settings.serviceUsers.confirmDelete");
        };
        self.restore = function (user) {
            return act("restore", user, { version: user.version }, self.canDevelop, "settings.serviceUsers.confirmRestore");
        };
        self.hardDelete = function (user) {
            return act("hard/delete", user, { version: user.version }, self.canDevelop, "settings.serviceUsers.confirmHardDelete");
        };
    };
});
