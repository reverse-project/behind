define(["ko", "models/auth", "models/session", "models/pager", "models/user"], function (ko, auth, session, createPager, user) {
    var pageSize = 10;

    var roleDefinitions = [
        { key: "administrator", label: "administrator", granted: "isAdministrator", allowed: "canChangeAdministrator" },
        { key: "developer", label: "developer", granted: "isDeveloper", allowed: "canChangeDeveloper" }
    ];

    return function UsersViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("User.Manage");
        });
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
                label: (granted ? "Remove " : "Make ") + definition.label,
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
                roles: item.roles,
                isCurrent: item.isCurrent,
                isBanned: item.ban !== null,
                banReason: item.ban ? item.ban.reason : "",
                canModerate: item.permissions.canModerate,
                roleActions: roleDefinitions.map(function (definition) {
                    return presentRoleAction(item, definition);
                }),
                banning: ko.observable(false),
                banMessage: ko.observable("")
            };

            row.hasActions = row.canModerate || row.roleActions.some(function (action) {
                return action.allowed;
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
            row.unban = function () {
                return perform(function () {
                    return auth.unbanUser(item.id);
                });
            };
            row.signOut = function () {
                if (!window.confirm("Sign " + row.name + " out of all sessions?")) {
                    return Promise.resolve();
                }

                return perform(function () {
                    return auth.signOutUser(item.id);
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

            return auth.loadUsers({ page: self.pager.page(), pageSize: pageSize, search: self.search().trim() })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.users([]);
                        return undefined;
                    }

                    if (self.pager.isOutOfRange(result.data)) {
                        self.pager.clampIfOutOfRange(result.data);
                        return self.refresh();
                    }

                    self.pager.update(result.data);
                    self.users(result.data.items.map(present));
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
            self.canManage.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                }
            });

            return self.refresh();
        };
    };
});
