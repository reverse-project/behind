define(["ko", "models/session", "models/schedules", "models/pager", "models/i18n", "models/paging"], function (ko, session, schedules, createPager, i18n, pagination) {
    var statusKeys = { running: "schedules.run.status.running", succeeded: "schedules.run.status.succeeded", failed: "schedules.run.status.failed" };
    var emailKeys = { notRequested: "schedules.email.status.notRequested", sent: "schedules.email.status.sent", failed: "schedules.email.status.failed", noRecipients: "schedules.email.status.noRecipients" };
    var dateTime = function (value, utc) { return value ? new Date(value).toLocaleString(undefined, utc ? { timeZone: "UTC", timeZoneName: "short" } : undefined) : ""; };
    var scheduleFromHash = function () {
        var query = (window.location.hash.split("?")[1] || "").split("&");
        var entry = query.find(function (part) { return part.indexOf("schedule=") === 0; });
        return entry ? decodeURIComponent(entry.slice("schedule=".length)) : "";
    };

    return function ScheduleRunsViewModel() {
        var self = this;
        var request = 0;
        self.isSignedIn = session.isSignedIn;
        self.i18n = i18n;
        self.canRead = ko.pureComputed(function () { return session.hasGrant("Schedule.Read"); });
        self.canDelete = ko.pureComputed(function () { return session.hasGrant("Schedule.Delete"); });
        self.canRawManage = ko.pureComputed(function () { return session.hasGrant("RawDatabase.Manage"); });
        self.isDenied = ko.pureComputed(function () { return session.isSignedIn() && session.grants() !== null && !self.canRead(); });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.items = ko.observableArray([]);
        self.scheduleId = ko.observable(scheduleFromHash());
        self.status = ko.observable("");
        self.schedules = ko.observableArray([]);
        self.actions = ko.observable({});
        self.statuses = ["running", "succeeded", "failed"].map(function (value) { return { value: value, label: ko.pureComputed(function () { return i18n.t(statusKeys[value]); }) }; });
        self.pager = createPager(function () { return self.refresh(); });
        self.pager.pageSize = 20;
        self.showDeleted = ko.observable(false);
        self.canViewDeleted = ko.pureComputed(function () { return session.hasGrant("RawDatabase.Manage"); });
        self.hasItems = ko.pureComputed(function () { return self.items().length > 0; });
        self.hasMessage = ko.pureComputed(function () { return self.message() !== ""; });

        self.refresh = function () {
            var current = ++request;
            if (!self.canRead()) {
                self.items([]);
                return Promise.resolve();
            }
            self.busy(true);
            return schedules.runs({ page: self.pager.page(), pageSize: 20, scheduleId: self.scheduleId(), status: self.status(), deleted: self.showDeleted() })
                .then(function (result) {
                    if (current !== request) { return; }
                    self.message(result.message);
                    if (!result.ok) {
                        self.items([]);
                        return;
                    }
                    if (self.pager.isOutOfRange(result.data)) {
                        self.pager.clampIfOutOfRange(result.data);
                        return self.refresh();
                    }
                    self.pager.update(result.data);
                    self.items(result.data.items.map(function (item) {
                        var statusClasses = { running: "text-bg-primary", succeeded: "text-bg-success", failed: "text-bg-danger" };
                        var run = Object.assign({}, item, {
                            scheduledForText: dateTime(item.scheduledFor, true),
                            startedAtText: dateTime(item.startedAt),
                            finishedAtText: dateTime(item.finishedAt),
                            statusClass: statusClasses[item.status] || "text-bg-secondary",
                            actionName: ko.pureComputed(function () { return i18n.tOr(self.actions()[item.action] || item.action, item.action); }),
                            emailText: i18n.t(emailKeys[item.email] || emailKeys.notRequested),
                            summaryText: item.summary || ""
                        });
                        run.remove = function () {
                            if (!window.confirm(i18n.t("schedules.confirm.deleteRun", [item.scheduleName]))) { return Promise.resolve(); }
                            return self.perform(function () { return schedules.removeRun(item.id); });
                        };
                        run.restore = function () { return self.perform(function () { return schedules.restoreRun(item.id); }); };
                        run.hardDelete = function () {
                            if (!window.confirm(i18n.t("schedules.confirm.hardDeleteRun", [item.scheduleName]))) { return Promise.resolve(); }
                            return self.perform(function () { return schedules.hardDeleteRun(item.id); });
                        };
                        return run;
                    }));
                }).finally(function () {
                    if (current === request) { self.busy(false); }
                });
        };
        self.perform = function (action) {
            self.busy(true);
            return action().then(function (result) {
                self.message(result.message);
                return result.ok ? self.refresh() : undefined;
            }).finally(function () { self.busy(false); });
        };
        self.find = function () {
            self.pager.page(1);
            return self.refresh();
        };
        self.showDeleted.subscribe(function () { self.pager.page(1); self.refresh(); });
        self.canViewDeleted.subscribe(function (allowed) {
            if (!allowed && self.showDeleted()) {
                self.showDeleted(false);
            }
        });
        self.status.subscribe(function () { self.pager.page(1); self.refresh(); });
        self.scheduleId.subscribe(function () { self.pager.page(1); self.refresh(); });
        self.activate = function () {
            schedules.actions().then(function (result) {
                if (result.ok) {
                    self.actions(result.data.reduce(function (items, action) {
                        items[action.key] = action.nameKey;
                        return items;
                    }, {}));
                }
            });
            pagination.all(schedules.manage, function (data) { return data.schedules; }).then(function (result) {
                if (result.ok) { self.schedules(result.data); }
            });
            return self.refresh();
        };
    };
});
