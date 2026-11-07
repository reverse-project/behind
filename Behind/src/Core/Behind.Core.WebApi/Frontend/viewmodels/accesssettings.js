define(["ko", "models/session", "models/accesssettings", "models/managedlist", "models/i18n", "bindings/richeditor"], function (ko, session, accessSettings, createManagedList, i18n) {
    return function AccessSettingsViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Access.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Access.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Access.Delete");
        });
        self.canViewDeleted = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.showDeleted = ko.observable(false);
        self.isDeleted = ko.observable(false);
        self.hasSettings = ko.observable(false);
        self.restrictionEnabled = ko.observable(false);
        self.restrictedToRole = ko.observable("");
        self.restrictionTitle = ko.observable("");
        self.restrictionMessage = ko.observable("");
        self.moderatedSignUp = ko.observable(false);
        self.roles = ko.observableArray([]);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });
        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && !self.isDeleted() && self.restrictedToRole() !== "";
        });
        self.canReset = ko.pureComputed(function () {
            return self.canDelete() && !self.busy() && !self.isDeleted();
        });
        self.canRestore = ko.pureComputed(function () {
            return self.canViewDeleted() && !self.busy() && self.isDeleted();
        });

        var apply = function (data) {
            self.roles(data.roles);
            self.restrictionEnabled(data.settings.restrictionEnabled);
            self.restrictedToRole(data.settings.restrictedToRole);
            self.restrictionTitle(data.settings.restrictionTitle);
            self.restrictionMessage(data.settings.restrictionMessage);
            self.moderatedSignUp(data.settings.moderatedSignUp);
            self.isDeleted(data.settings.isDeleted);
        };

        var load = function () {
            if (!self.canRead()) {
                return Promise.resolve();
            }

            self.busy(true);

            return accessSettings.manage(self.showDeleted())
                .then(function (result) {
                    self.message(result.message);
                    self.hasSettings(result.ok);

                    if (result.ok) {
                        apply(result.data);
                    }
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        var perform = function (action, afterwards) {
            self.busy(true);

            return action()
                .then(function (result) {
                    return (result.ok ? afterwards() : Promise.resolve()).then(function () {
                        self.message(result.message);
                    });
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        var present = function (item) {
            var row = {
                id: item.id,
                displayName: item.displayName,
                emails: item.emails.join(", ")
            };

            row.approve = function () {
                return self.pending.perform(function () {
                    return accessSettings.approve(row.id);
                });
            };
            row.reject = function () {
                if (!window.confirm(i18n.t("accessSettings.confirm.reject", [row.displayName || row.emails]))) {
                    return Promise.resolve();
                }

                return self.pending.perform(function () {
                    return accessSettings.reject(row.id);
                });
            };

            return row;
        };

        self.pending = createManagedList({
            allowed: self.canRead,
            load: accessSettings.pendingUsers,
            pageOf: function (data) {
                return data.users;
            },
            present: present
        });

        self.uploadImage = function (file) {
            return accessSettings.uploadImage(file).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return null;
                }

                return result.data.url;
            });
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            return perform(
                function () {
                    return accessSettings.save({
                        restrictionEnabled: self.restrictionEnabled(),
                        restrictedToRole: self.restrictedToRole(),
                        restrictionTitle: self.restrictionTitle().trim(),
                        restrictionMessage: self.restrictionMessage(),
                        moderatedSignUp: self.moderatedSignUp()
                    });
                },
                load
            );
        };

        self.reset = function () {
            if (!self.canReset() || !window.confirm(i18n.t("accessSettings.confirm.delete"))) {
                return Promise.resolve();
            }

            return perform(accessSettings.remove, load);
        };

        self.restore = function () {
            if (!self.canRestore()) {
                return Promise.resolve();
            }

            return perform(accessSettings.restore, function () {
                self.showDeleted(false);

                return Promise.resolve();
            });
        };

        self.hardDelete = function () {
            if (!self.canRestore() || !window.confirm(i18n.t("accessSettings.confirm.hardDelete"))) {
                return Promise.resolve();
            }

            return perform(accessSettings.hardDelete, function () {
                self.showDeleted(false);

                return Promise.resolve();
            });
        };

        self.activate = function () {
            self.showDeleted.subscribe(load);
            self.canRead.subscribe(function (allowed) {
                if (allowed) {
                    load();
                }
            });
            self.canViewDeleted.subscribe(function (allowed) {
                if (!allowed && self.showDeleted()) {
                    self.showDeleted(false);
                }
            });

            return Promise.all([load(), self.pending.activate()]);
        };
    };
});
