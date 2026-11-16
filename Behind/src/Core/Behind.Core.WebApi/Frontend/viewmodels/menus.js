define(["ko", "models/session", "models/menus", "models/managedlist", "models/i18n"], function (ko, session, menus, createManagedList, i18n) {
    return function MenusViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Menu.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Menu.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Menu.Delete");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        self.editingId = ko.observable(null);
        self.editingBuiltIn = ko.observable(false);
        self.name = ko.observable("");
        self.title = ko.observable("");

        self.isEditing = ko.pureComputed(function () {
            return self.editingId() !== null;
        });

        self.heading = ko.pureComputed(function () {
            return self.isEditing() ? i18n.t("menus.editor.edit") : i18n.t("menus.editor.new");
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.name().trim() !== "" && self.title().trim() !== "";
        });

        var present = function (item) {
            var row = {
                id: item.id,
                name: item.name,
                title: item.title,
                label: ko.pureComputed(function () {
                    return menus.label(item.title);
                }),
                itemCount: item.itemCount,
                isBuiltIn: item.isBuiltIn,
                isDeleted: item.isDeleted,
                itemsHref: "#/manage/menus/" + item.id
            };

            row.edit = function () {
                self.editingId(row.id);
                self.editingBuiltIn(row.isBuiltIn);
                self.name(row.name);
                self.title(row.title);
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("menus.confirm.delete", [row.label()]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return menus.remove(row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return menus.restore(row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("menus.confirm.hardDelete", [row.label()]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return menus.hardDelete(row.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: menus.manage,
            pageOf: function (data) {
                return data.menus;
            },
            present: present
        });

        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            self.editingBuiltIn(false);
            self.name("");
            self.title("");
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = { name: self.name().trim(), title: self.title().trim() };

            return self.list.perform(function () {
                var saving = self.isEditing() ? menus.save(self.editingId(), fields) : menus.create(fields);

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
