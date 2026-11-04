define(["ko", "models/auth", "models/session", "models/access", "models/managedlist", "models/i18n"], function (ko, auth, session, access, createManagedList, i18n) {
    return function RolesViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("Role.Manage");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canManage();
        });
        self.newName = ko.observable("");
        self.newIsDeveloper = ko.observable(false);
        self.canCreateDeveloperRole = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });

        self.canCreate = ko.pureComputed(function () {
            return self.newName().trim() !== "" && !self.busy();
        });

        var present = function (item, data) {
            var row = {
                id: item.id,
                name: item.name,
                isBuiltIn: item.isBuiltIn,
                isDeleted: item.isDeleted,
                isDeveloper: item.isDeveloper,
                canSetDeveloper: item.canSetDeveloper,
                grants: item.grants,
                restrictions: item.restrictions,
                canEdit: item.canEdit,
                canDelete: item.canDelete,
                editing: ko.observable(false),
                editor: access.create()
            };

            row.edit = function () {
                row.editor.load(data.availableGrants, row.grants, row.restrictions);
                row.editing(true);
            };
            row.cancel = function () {
                row.editing(false);
            };
            row.save = function () {
                return self.list.perform(function () {
                    return auth.saveRoleAccess(row.id, row.editor.request());
                });
            };
            row.toggleDeveloper = function () {
                return self.list.perform(function () {
                    return auth.setRoleDeveloper(row.id, !row.isDeveloper);
                });
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("roles.confirmDelete", [row.name]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return auth.deleteRole(row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return auth.restoreRole(row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("roles.confirmHardDelete", [row.name]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return auth.hardDeleteRole(row.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canManage,
            load: auth.loadRoles,
            pageOf: function (data) {
                return data.roles;
            },
            present: present
        });

        self.busy = self.list.busy;
        self.message = self.list.message;
        self.hasMessage = self.list.hasMessage;
        self.roles = self.list.items;
        self.hasRoles = self.list.hasItems;
        self.refresh = self.list.refresh;

        self.create = function () {
            if (!self.canCreate()) {
                return Promise.resolve();
            }

            return self.list.perform(function () {
                return auth.createRole(self.newName().trim(), self.newIsDeveloper()).then(function (result) {
                    if (result.ok) {
                        self.newName("");
                        self.newIsDeveloper(false);
                    }

                    return result;
                });
            });
        };

        self.activate = self.list.activate;
    };
});
