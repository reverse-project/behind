define(["ko", "models/auth", "models/session", "models/pager", "models/letterpager", "models/user", "models/i18n", "viewmodels/useraccess"], function (ko, auth, session, createPager, createLetterPager, user, i18n, UserAccessViewModel) {
    var pageSize = 10;

    var roleDefinitions = [
        { key: "administrator", grantedLabel: "users.removeAdministrator", grantLabel: "users.makeAdministrator", granted: "isAdministrator", allowed: "canChangeAdministrator" },
        { key: "developer", grantedLabel: "users.removeDeveloper", grantLabel: "users.makeDeveloper", granted: "isDeveloper", allowed: "canChangeDeveloper" }
    ];

    return function UsersViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("User.Manage");
        });
        self.canViewDeleted = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.showDeleted = ko.observable(false);
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canManage();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.search = ko.observable("");
        self.users = ko.observableArray([]);
        self.pager = createPager(function () {
            return self.refresh();
        });
        self.letterPager = createLetterPager(function () {
            self.pager.page(1);
            return self.refresh();
        });
        self.showDeleted.subscribe(function () {
            self.letterPager.selected("");
            self.pager.page(1);
            self.refresh();
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.hasUsers = ko.pureComputed(function () {
            return self.users().length > 0;
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

        var presentRoleAction = function (item, definition) {
            var granted = item[definition.granted];

            return {
                label: ko.pureComputed(function () {
                    return i18n.t(granted ? definition.grantedLabel : definition.grantLabel);
                }),
                granted: granted,
                allowed: item.permissions[definition.allowed],
                run: function () {
                    return perform(function () {
                        return granted
                            ? auth.deelevateUser(item.id, definition.key)
                            : auth.elevateUser(item.id, definition.key);
                    });
                }
            };
        };

        var present = function (item) {
            var row = {
                id: item.id,
                name: user.label(item),
                emails: item.emails.join(", "),
                isEmailUnconfirmed: !item.emailConfirmed,
                canConfirmEmail: item.permissions.canConfirmEmail,
                roles: item.roles,
                isCurrent: item.isCurrent,
                isBanned: item.ban !== null,
                banReason: item.ban ? item.ban.reason : "",
                canModerate: item.permissions.canModerate,
                isDeleted: item.isDeleted,
                canDelete: item.permissions.canDelete,
                canManageDeleted: item.permissions.canManageDeleted,
                canImpersonate: item.permissions.canImpersonate,
                roleActions: roleDefinitions.map(function (definition) {
                    return presentRoleAction(item, definition);
                }),
                banning: ko.observable(false),
                banMessage: ko.observable("")
            };

            row.access = new UserAccessViewModel(item.id, function (body) {
                return perform(function () {
                    return auth.saveUserAccess(item.id, body);
                });
            });

            row.canConfirmBan = ko.pureComputed(function () {
                return row.banMessage().trim() !== "";
            });
            row.startBan = function () {
                row.banning(true);
            };
            row.cancelBan = function () {
                row.banning(false);
                row.banMessage("");
            };
            row.confirmBan = function () {
                return perform(function () {
                    return auth.banUser(item.id, row.banMessage().trim());
                });
            };
            row.confirmEmail = function () {
                return perform(function () {
                    return auth.confirmUserEmail(item.id);
                });
            };
            row.unban = function () {
                return perform(function () {
                    return auth.unbanUser(item.id);
                });
            };
            row.signOut = function () {
                if (!window.confirm(i18n.t("users.confirmSignOut", [row.name]))) {
                    return Promise.resolve();
                }

                return perform(function () {
                    return auth.signOutUser(item.id);
                });
            };

            row.impersonate = function () {
                if (!window.confirm(i18n.t("users.confirmImpersonate", [row.name]))) {
                    return Promise.resolve();
                }

                self.busy(true);

                return auth.impersonateUser(item.id)
                    .then(function (result) {
                        if (result.ok) {
                            window.location.hash = "#/";
                            return;
                        }

                        self.message(result.message);
                    })
                    .finally(function () {
                        self.busy(false);
                    });
            };

            row.remove = function () {
                if (!window.confirm(i18n.t("users.confirmDelete", [row.name]))) {
                    return Promise.resolve();
                }

                return perform(function () {
                    return auth.deleteUser(item.id);
                });
            };
            row.restore = function () {
                return perform(function () {
                    return auth.restoreUser(item.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("users.confirmHardDelete", [row.name]))) {
                    return Promise.resolve();
                }

                return perform(function () {
                    return auth.hardDeleteUser(item.id);
                });
            };

            return row;
        };

        self.refresh = function () {
            if (!self.canManage()) {
                self.users([]);
                return Promise.resolve();
            }

            self.busy(true);

            return auth.loadUsers({
                page: self.pager.page(),
                pageSize: pageSize,
                search: self.search().trim(),
                letter: self.letterPager.selected(),
                deleted: self.showDeleted()
            })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.users([]);
                        return undefined;
                    }

                    self.letterPager.update(result.data.letters);

                    if (self.pager.isOutOfRange(result.data.users)) {
                        self.pager.clampIfOutOfRange(result.data.users);
                        return self.refresh();
                    }

                    self.pager.update(result.data.users);
                    self.users(result.data.users.items.map(present));
                    return undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.find = function () {
            self.pager.page(1);
            return self.refresh();
        };

        self.activate = function () {
            self.canViewDeleted.subscribe(function (allowed) {
                if (!allowed && self.showDeleted()) {
                    self.showDeleted(false);
                }
            });

            self.canManage.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                }
            });

            return self.refresh();
        };
    };
});
