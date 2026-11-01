define(["ko", "models/auth", "models/session", "models/access"], function (ko, auth, session, access) {
    return function RolesViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("Role.Manage");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canManage();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.newName = ko.observable("");
        self.roles = ko.observableArray([]);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.hasRoles = ko.pureComputed(function () {
            return self.roles().length > 0;
        });

        self.canCreate = ko.pureComputed(function () {
            return self.newName().trim() !== "" && !self.busy();
        });

        var perform = function (action) {
            self.busy(true);

            return action()
                .then(function (result) {
                    return (result.ok ? self.refresh() : Promise.resolve()).then(function () {
                        self.message(result.message);
                    });
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        var present = function (item, available) {
            var row = {
                id: item.id,
                name: item.name,
                isBuiltIn: item.isBuiltIn,
                grants: item.grants,
                restrictions: item.restrictions,
                canEdit: item.canEdit,
                canDelete: item.canDelete,
                editing: ko.observable(false),
                editor: access.create()
            };

            row.edit = function () {
                row.editor.load(available, row.grants, row.restrictions);
                row.editing(true);
            };
            row.cancel = function () {
                row.editing(false);
            };
            row.save = function () {
                return perform(function () {
                    return auth.saveRoleAccess(row.id, row.editor.request());
                });
            };
            row.remove = function () {
                if (!window.confirm("Delete the " + row.name + " role? Users lose the access it gives.")) {
                    return Promise.resolve();
                }

                return perform(function () {
                    return auth.deleteRole(row.id);
                });
            };

            return row;
        };

        self.refresh = function () {
            if (!self.canManage()) {
                self.roles([]);
                return Promise.resolve();
            }

            self.busy(true);

            return auth.loadRoles()
                .then(function (result) {
                    self.message(result.message);

                    self.roles(result.ok
                        ? result.data.roles.map(function (item) {
                            return present(item, result.data.availableGrants);
                        })
                        : []);
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.create = function () {
            if (!self.canCreate()) {
                return Promise.resolve();
            }

            return perform(function () {
                return auth.createRole(self.newName().trim()).then(function (result) {
                    if (result.ok) {
                        self.newName("");
                    }

                    return result;
                });
            });
        };

        self.activate = function () {
            self.canManage.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                }
            });

            return self.refresh();
        };
    };
});
