define(["ko", "models/http", "models/i18n", "models/session"], function (ko, http, i18n, session) {
    return function ContentRestrictionsViewModel() {
        var self = this;
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.types = ko.observableArray([]);
        self.items = ko.observableArray([]);
        self.entityType = ko.observable("");
        self.entityId = ko.observable("");
        self.minimumAge = ko.observable(0);
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.enabled = ko.observable(false);
        self.showDeleted = ko.observable(false);
        self.browserOpen = ko.observable(false);
        self.browserBusy = ko.observable(false);
        self.browserSearch = ko.observable("");
        self.browserItems = ko.observableArray([]);
        self.browserPage = ko.observable(1);
        self.browserPageCount = ko.observable(0);
        self.browserMessage = ko.observable("");
        self.selectedName = ko.observable("");
        var browserRequest = 0;
        var disposed = false;
        self.browse = function () {
            var type = self.entityType();
            var request = ++browserRequest;
            self.browserItems([]);
            self.browserMessage("");
            if (!self.canManage() || !type) {
                return Promise.resolve();
            }
            self.browserOpen(true);
            self.browserBusy(true);
            return http.get("/content-restrictions/browse/" + encodeURIComponent(type)
                + "?page=" + self.browserPage() + "&search=" + encodeURIComponent(self.browserSearch().trim()))
                .then(function (response) {
                    if (disposed || request !== browserRequest) {
                        return;
                    }
                    if (!response.ok) {
                        self.browserMessage(i18n.t("contentRestrictions.failed"));
                        return;
                    }
                    self.browserItems(response.data.items);
                    self.browserPageCount(response.data.pageCount);
                    if (!response.data.items.length) {
                        self.browserMessage(i18n.t("contentRestrictions.empty"));
                    }
                }).catch(function (error) {
                    if (!disposed && request === browserRequest) {
                        self.browserMessage(error.message);
                    }
                }).finally(function () {
                    if (!disposed && request === browserRequest) {
                        self.browserBusy(false);
                    }
                });
        };
        self.findItems = function () {
            self.browserPage(1);
            return self.browse();
        };
        self.previousItems = function () {
            if (self.browserPage() > 1) {
                self.browserPage(self.browserPage() - 1);
                return self.browse();
            }
        };
        self.nextItems = function () {
            if (self.browserPage() < self.browserPageCount()) {
                self.browserPage(self.browserPage() + 1);
                return self.browse();
            }
        };
        self.chooseItem = function (item) {
            self.entityId(item.id);
            self.selectedName(item.displayName);
            self.browserOpen(false);
        };
        self.select = function (item) {
            self.entityType(item.entityType);
            self.entityId(item.entityId);
            self.selectedName(item.displayName || item.entityId);
            self.minimumAge(item.minimumAge);
        };
        var load = function () {
            return http.get("/content-restrictions?deleted=" + self.showDeleted()).then(function (response) {
                if (response.ok) {
                    self.enabled(response.data.enabled);
                    self.types(response.data.types);
                    self.items(response.data.items);
                } else {
                    self.message(i18n.t("contentRestrictions.failed"));
                }
            });
        };
        var change = function (item, action) {
            self.busy(true);
            return http.post("/content-restrictions/" + item.id + "/" + action).then(function (response) {
                if (response.ok) {
                    return load();
                }
                self.message(i18n.t("contentRestrictions.failed"));
            }).finally(function () {
                self.busy(false);
            });
        };
        self.remove = function (item) { return change(item, "delete"); };
        self.restore = function (item) { return change(item, "restore"); };
        self.hardDelete = function (item) { return change(item, "hard/delete"); };
        self.save = function () {
            self.busy(true);
            self.message("");
            return http.post("/content-restrictions", {
                entityType: self.entityType(),
                entityId: self.entityId().trim(),
                minimumAge: Number(self.minimumAge())
            }).then(function (response) {
                self.message(i18n.t(response.ok ? "contentRestrictions.saved" : "contentRestrictions.failed"));
                if (response.ok) {
                    return load();
                }
            }).finally(function () {
                self.busy(false);
            });
        };
        var deletedSubscription = self.showDeleted.subscribe(load);
        var idSubscription = self.entityId.subscribe(function () {
            self.selectedName("");
        });
        var typeSubscription = self.entityType.subscribe(function () {
            ++browserRequest;
            self.browserBusy(false);
            self.entityId("");
            self.selectedName("");
            self.browserItems([]);
            self.browserSearch("");
            self.browserPage(1);
            if (self.browserOpen()) {
                self.browse();
            }
        });
        self.activate = load;
        self.dispose = function () {
            disposed = true;
            ++browserRequest;
            typeSubscription.dispose();
            idSubscription.dispose();
            deletedSubscription.dispose();
            self.canManage.dispose();
        };
    };
});
