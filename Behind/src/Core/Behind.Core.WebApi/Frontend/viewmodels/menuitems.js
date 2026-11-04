define(["ko", "models/session", "models/menus", "models/managedlist", "models/i18n"], function (ko, session, menus, createManagedList, i18n) {
    return function MenuItemsViewModel(context) {
        var self = this;
        var menuId = context.params.id;

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

        self.menu = ko.observable(null);
        self.allowsUserItems = ko.observable(false);
        self.parents = ko.observableArray([]);
        self.roles = ko.observableArray([]);
        self.targets = ko.pureComputed(function () {
            return menus.targets.map(function (target) {
                return { value: target.value, label: i18n.t(target.label) };
            });
        });

        self.noParentLabel = ko.pureComputed(function () {
            return i18n.t("menuitems.noParent");
        });

        self.editingId = ko.observable(null);
        self.title = ko.observable("");
        self.url = ko.observable("");
        self.target = ko.observable("CurrentPage");
        self.kind = ko.observable("Link");
        self.position = ko.observable(0);
        self.parentId = ko.observable(undefined);
        self.roleIds = ko.observableArray([]);

        self.hasMenu = ko.pureComputed(function () {
            return self.menu() !== null;
        });

        self.menuTitle = ko.pureComputed(function () {
            return self.hasMenu() ? menus.label(self.menu().title) : i18n.t("menuitems.menu");
        });

        self.isEditing = ko.pureComputed(function () {
            return self.editingId() !== null;
        });

        self.heading = ko.pureComputed(function () {
            return self.isEditing() ? i18n.t("menuitems.editor.edit") : i18n.t("menuitems.editor.new");
        });

        self.isUserItem = ko.pureComputed(function () {
            return self.kind() === "User";
        });

        self.hasRoles = ko.pureComputed(function () {
            return self.roles().length > 0;
        });

        self.parentChoices = ko.pureComputed(function () {
            var editing = self.editingId();

            return self.isUserItem() ? [] : self.parents().filter(function (parent) {
                return parent.id !== editing;
            }).map(function (parent) {
                return { id: parent.id, title: menus.label(parent.title) };
            });
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.title().trim() !== "" && self.url().trim() !== "";
        });

        var labelOf = function (list, id, fallback) {
            return list.filter(function (entry) {
                return entry.id === id;
            }).map(function (entry) {
                return entry.name || entry.title;
            })[0] || fallback;
        };

        var rolesLabel = function (ids) {
            return ids.length === 0 ? i18n.t("menuitems.allRoles") : ids.map(function (id) {
                return labelOf(self.roles(), id, id);
            }).join(", ");
        };

        var present = function (item) {
            var row = {
                id: item.id,
                title: item.title,
                label: ko.pureComputed(function () {
                    return menus.label(item.title);
                }),
                url: item.url,
                kind: item.kind,
                isDeleted: item.isDeleted,
                isSub: !!item.parentId,
                parentTitle: ko.pureComputed(function () {
                    return item.parentId ? menus.label(labelOf(self.parents(), item.parentId, "")) : "";
                }),
                isUser: item.kind === "User",
                targetLabel: ko.pureComputed(function () {
                    return labelOf(self.targets().map(function (target) {
                        return { id: target.value, title: target.label };
                    }), item.target, item.target);
                }),
                rolesLabel: ko.pureComputed(function () {
                    return rolesLabel(item.roleIds);
                })
            };

            row.edit = function () {
                self.editingId(item.id);
                self.title(item.title);
                self.url(item.url);
                self.target(item.target);
                self.kind(item.kind);
                self.position(item.position);
                self.parentId(item.parentId || undefined);
                self.roleIds(item.roleIds.slice());
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("menuitems.confirm.delete", [row.label()]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return menus.removeItem(row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return menus.restoreItem(row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("menuitems.confirm.hardDelete", [row.label()]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return menus.hardDeleteItem(row.id);
                });
            };

            return row;
        };

        var load = function (query) {
            return menus.manageItems(menuId, query).then(function (result) {
                self.menu(result.ok ? result.data.menu : null);

                if (result.ok) {
                    self.allowsUserItems(result.data.allowsUserItems);
                    self.parents(result.data.parents.map(function (parent) {
                        return { id: parent.id, title: parent.title };
                    }));
                    self.roles(result.data.roles);
                }

                return result;
            });
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: load,
            pageOf: function (data) {
                return data.items;
            },
            present: present
        });

        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            self.title("");
            self.url("");
            self.target("CurrentPage");
            self.kind("Link");
            self.position(0);
            self.parentId(undefined);
            self.roleIds([]);
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = {
                title: self.title().trim(),
                url: self.url().trim(),
                target: self.target(),
                kind: self.kind(),
                position: parseInt(self.position(), 10) || 0,
                parentId: self.isUserItem() ? null : self.parentId(),
                roleIds: self.roleIds()
            };

            return self.list.perform(function () {
                var saving = self.isEditing() ? menus.saveItem(self.editingId(), fields) : menus.createItem(menuId, fields);

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
