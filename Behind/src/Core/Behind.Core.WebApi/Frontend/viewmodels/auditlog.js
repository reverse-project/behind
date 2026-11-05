define(["ko", "models/auth", "models/session", "models/pager", "models/i18n"], function (ko, auth, session, createPager, i18n) {
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

    var entityKeys = {
        HumanUser: "audit.entities.HumanUser",
        User: "audit.entities.User",
        UserRole: "audit.entities.UserRole",
        Role: "audit.entities.Role",
        Grant: "audit.entities.Grant",
        Restriction: "audit.entities.Restriction",
        Email: "audit.entities.Email",
        Password: "audit.entities.Password",
        ValidSession: "audit.entities.ValidSession",
        Session: "audit.entities.Session",
        UserAgent: "audit.entities.UserAgent",
        MagicLink: "audit.entities.MagicLink",
        WallOfShameEntry: "audit.entities.WallOfShameEntry",
        Site: "audit.entities.Site",
        News: "audit.entities.News",
        NewsCategory: "audit.entities.NewsCategory",
        NewsComment: "audit.entities.NewsComment",
        Article: "audit.entities.Article",
        ArticleCategory: "audit.entities.ArticleCategory",
        Menu: "audit.entities.Menu",
        MenuItem: "audit.entities.MenuItem",
        RouteAlias: "audit.entities.RouteAlias",
        Gallery: "audit.entities.Gallery",
        GalleryItem: "audit.entities.GalleryItem",
        Carousel: "audit.entities.Carousel",
        CarouselSlide: "audit.entities.CarouselSlide",
        SiteSection: "audit.entities.SiteSection",
        VfsFile: "audit.entities.VfsFile",
        DownloadFolder: "audit.entities.DownloadFolder",
        Download: "audit.entities.Download"
    };

    var known = function (keys, name) {
        return ko.pureComputed(function () {
            return i18n.tOr(keys[name], name);
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
            actorEmails: item.actor.emails.join(", "),
            hasBehalfOf: item.onBehalfOf !== null,
            behalfOf: item.onBehalfOf ? item.onBehalfOf.label : "",
            action: known(actionKeys, item.action),
            actionClass: actionClasses[item.action] || "text-bg-secondary",
            entityName: known(entityKeys, item.entityName),
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
        self.oldestFirst = ko.observable(false);
        self.orders = [
            {
                label: ko.pureComputed(function () {
                    return i18n.t("audit.newestFirst");
                }),
                oldestFirst: false
            },
            {
                label: ko.pureComputed(function () {
                    return i18n.t("audit.oldestFirst");
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

        self.reset = function () {
            self.fromDate(defaultFrom());
            self.toDate(defaultTo());
            self.search("");
            self.oldestFirst(false);
            return self.find();
        };

        self.activate = function () {
            self.canView.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                }
            });

            return self.refresh();
        };
    };
});
