define(["ko", "models/auth", "models/session", "models/pager", "models/i18n", "models/user"], function (ko, auth, session, createPager, i18n, user) {
    var pageSize = 20;
    var defaultRangeDays = 7;

    var actionClasses = {
        Created: "text-bg-success",
        Updated: "text-bg-primary",
        Deleted: "text-bg-warning",
        HardDeleted: "text-bg-danger",
        Restored: "text-bg-info",
        ChangeOwnership: "text-bg-secondary",
        Moved: "text-bg-secondary",
        ImpersonationStarted: "text-bg-warning",
        ImpersonationEnded: "text-bg-warning"
    };

    var actionKeys = {
        Created: "audit.actions.Created",
        Updated: "audit.actions.Updated",
        Deleted: "audit.actions.Deleted",
        HardDeleted: "audit.actions.HardDeleted",
        Restored: "audit.actions.Restored",
        ChangeOwnership: "audit.actions.ChangeOwnership",
        Moved: "audit.actions.Moved",
        ImpersonationStarted: "audit.actions.ImpersonationStarted",
        ImpersonationEnded: "audit.actions.ImpersonationEnded"
    };

    var known = function (keys, name) {
        return ko.pureComputed(function () {
            return i18n.tOr(keys[name], name);
        });
    };

    var entityName = function (name) {
        return ko.pureComputed(function () {
            return i18n.tOr("audit.entities." + name, name);
        });
    };

    var shown = function (value) {
        return ko.pureComputed(function () {
            return value === null ? i18n.t("audit.none") : value;
        });
    };

    var pad = function (number) {
        return (number < 10 ? "0" : "") + number;
    };

    var toDateInput = function (date) {
        return date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate());
    };

    var defaultFrom = function () {
        var date = new Date();
        date.setDate(date.getDate() - defaultRangeDays);
        return toDateInput(date);
    };

    var defaultTo = function () {
        return toDateInput(new Date());
    };

    var present = function (item) {
        return {
            timestamp: ko.pureComputed(function () {
                return i18n.dateTime(item.timestamp);
            }),
            actor: item.actor.label,
            actorHref: user.href(item.actor.id),
            actorEmails: item.actor.emails.join(", "),
            hasBehalfOf: item.onBehalfOf !== null,
            behalfOf: item.onBehalfOf ? item.onBehalfOf.label : "",
            behalfOfHref: item.onBehalfOf ? user.href(item.onBehalfOf.id) : null,
            action: known(actionKeys, item.action),
            actionClass: actionClasses[item.action] || "text-bg-secondary",
            entityName: entityName(item.entityName),
            entityKey: item.entityKey,
            entityLabel: item.entityLabel,
            changes: item.changes.map(function (change) {
                return {
                    property: change.property,
                    oldValue: shown(change.oldValue),
                    newValue: shown(change.newValue)
                };
            })
        };
    };

    return function AuditLogViewModel() {
        var self = this;
        var entityTypesLoaded = false;

        self.isSignedIn = session.isSignedIn;
        self.canView = ko.pureComputed(function () {
            return session.hasGrant("AuditLog.Read");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canView();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.fromDate = ko.observable(defaultFrom());
        self.toDate = ko.observable(defaultTo());
        self.search = ko.observable("");
        self.entity = ko.observable("");
        self.entityTypes = ko.observableArray([{ value: "", label: ko.pureComputed(function () {
            return i18n.t("audit.allEntities");
        }) }]);
        self.action = ko.observable("");
        self.actor = ko.observable("");
        self.behalfOf = ko.observable("");
        self.sortBy = ko.observable("timestamp");
        self.oldestFirst = ko.observable(false);
        self.actions = [{ value: "", label: ko.pureComputed(function () {
            return i18n.t("audit.allActions");
        }) }].concat(Object.keys(actionKeys).map(function (name) {
            return { value: name, label: known(actionKeys, name) };
        }));
        self.sortFields = ["timestamp", "entity", "key", "action", "actor"].map(function (name) {
            return {
                value: name,
                label: ko.pureComputed(function () {
                    return i18n.t("audit.sort." + name);
                })
            };
        });
        self.orders = [
            {
                label: ko.pureComputed(function () {
                    return i18n.t("audit.descending");
                }),
                oldestFirst: false
            },
            {
                label: ko.pureComputed(function () {
                    return i18n.t("audit.ascending");
                }),
                oldestFirst: true
            }
        ];
        self.entries = ko.observableArray([]);
        self.pager = createPager(function () {
            return self.refresh();
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.hasEntries = ko.pureComputed(function () {
            return self.entries().length > 0;
        });

        self.hasValidRange = ko.pureComputed(function () {
            return self.fromDate() !== "" && self.toDate() !== "" && self.fromDate() <= self.toDate();
        });

        self.canSearch = ko.pureComputed(function () {
            return !self.busy() && self.hasValidRange();
        });

        self.refresh = function () {
            if (!self.canView()) {
                self.entries([]);
                return Promise.resolve();
            }

            if (!self.hasValidRange()) {
                self.entries([]);
                self.message(i18n.t("audit.invalidRange"));
                return Promise.resolve();
            }

            self.busy(true);

            return auth.loadAuditLog({
                fromDate: self.fromDate(),
                toDate: self.toDate(),
                search: self.search().trim(),
                entity: self.entity().trim(),
                action: self.action(),
                actor: self.actor().trim(),
                behalfOf: self.behalfOf().trim(),
                sortBy: self.sortBy(),
                descending: !self.oldestFirst(),
                oldestFirst: self.oldestFirst(),
                page: self.pager.page(),
                pageSize: pageSize
            })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.entries([]);
                        return undefined;
                    }

                    if (self.pager.isOutOfRange(result.data)) {
                        self.pager.clampIfOutOfRange(result.data);
                        return self.refresh();
                    }

                    self.pager.update(result.data);
                    self.entries(result.data.items.map(present));
                    if (entityTypesLoaded) {
                        return undefined;
                    }

                    return auth.loadAuditEntityTypes().then(function (types) {
                        if (!self.canView()) {
                            return;
                        }
                        if (!types.ok) {
                            self.message(types.message);
                            return;
                        }
                        self.entityTypes.splice(1, self.entityTypes().length - 1);
                        types.data.forEach(function (name) {
                            self.entityTypes.push({ value: name, label: entityName(name) });
                        });
                        entityTypesLoaded = true;
                    });
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.find = function () {
            self.pager.page(1);
            return self.refresh();
        };

        self.reset = function () {
            self.fromDate(defaultFrom());
            self.toDate(defaultTo());
            self.search("");
            self.entity("");
            self.action("");
            self.actor("");
            self.behalfOf("");
            self.sortBy("timestamp");
            self.oldestFirst(false);
            return self.find();
        };

        self.activate = function () {
            self.canView.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                } else {
                    entityTypesLoaded = false;
                    self.entity("");
                    self.entityTypes.splice(1, self.entityTypes().length - 1);
                    self.entries([]);
                }
            });

            return self.refresh();
        };
    };
});
