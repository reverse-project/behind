define(["ko", "models/session", "models/privateapi", "models/pager", "models/i18n"], function (ko, session, api, createPager, i18n) {
    ko.bindingHandlers.reportsDispose = {
        init: function (element, valueAccessor) {
            ko.utils.domNodeDisposal.addDisposeCallback(element, function () { valueAccessor().dispose(); });
        }
    };
    return function ReportsViewModel() {
        var self = this;
        var alive = true;
        var listTicket = 0;
        var detailTicket = 0;
        self.t = i18n.t;
        self.dateTime = i18n.dateTime;
        self.allowed = ko.pureComputed(function () { return session.hasGrant("Report.Manage"); });
        self.developer = ko.pureComputed(function () { return session.hasGrant("RawDatabase.Manage"); });
        self.status = ko.observable("open");
        self.showDeleted = ko.observable(false);
        self.items = ko.observableArray([]);
        self.detail = ko.observable(null);
        self.conversation = ko.observable(null);
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.banUntil = ko.observable("");
        self.reporterMessage = ko.observable("");
        self.reportedEmail = ko.observable("");
        var deliveryLabels = {
            notRequested: "reports.delivery.notRequested",
            failed: "reports.delivery.failed",
            sent: "reports.delivery.sent",
            pending: "reports.delivery.pending",
            sending: "reports.delivery.sending"
        };
        var sourceLabels = {
            profile: "reports.source.profile",
            forumPost: "reports.source.forumPost",
            chatMessage: "reports.source.chatMessage",
            newsComment: "reports.source.newsComment",
            privateMessage: "reports.source.privateMessage"
        };
        var statusLabels = { open: "reports.open", resolved: "reports.resolved", dismissed: "reports.dismissed" };
        self.deliveryLabel = function (status) {
            return deliveryLabels[status] ? i18n.t(deliveryLabels[status]) : status || "";
        };
        self.sourceLabel = function (source) {
            return sourceLabels[source] ? i18n.t(sourceLabels[source]) : source;
        };
        self.statusLabel = function (status) {
            return statusLabels[status] ? i18n.t(statusLabels[status]) : status;
        };
        self.pager = createPager(function () { return self.load(); });
        var identity = session.accessToken();
        var identitySubscription = session.grants.subscribe(function () {
            if (identity === session.accessToken()) {
                return;
            }
            identity = session.accessToken();
            listTicket++; detailTicket++;
            self.items([]); self.detail(null); self.conversation(null);
            self.reporterMessage(""); self.reportedEmail(""); self.message("");
        });
        var fail = function (response) {
            self.message(api.error(response));
        };
        var error = function () {
            if (alive) {
                self.message(i18n.t("privateMessages.requestFailed"));
            }
        };
        self.load = function () {
            if (!self.allowed()) {
                return Promise.resolve();
            }
            var ticket = ++listTicket;
            var deleted = self.developer() && self.showDeleted();
            return api.get("/reports?" + api.query({ page: self.pager.page(), pageSize: 20, status: self.status(), deleted: deleted })).then(function (response) {
                if (!alive || ticket !== listTicket) {
                    return;
                }
                if (!response.ok) {
                    fail(response);
                    return;
                }
                self.items(response.data.items || []);
                self.pager.update({ page: response.data.page || response.data.pageNumber || self.pager.page(), pageCount: response.data.pageCount, total: response.data.total });
            }).catch(error);
        };
        self.changeFilter = function () {
            self.pager.page(1);
            self.detail(null);
            self.conversation(null);
            detailTicket++;
            return self.load();
        };
        self.open = function (item) {
            if (!self.allowed()) {
                return Promise.resolve();
            }
            var ticket = ++detailTicket;
            var deleted = self.showDeleted();
            self.detail(null);
            self.conversation(null);
            return api.get("/reports/" + encodeURIComponent(item.id) + (deleted ? "?deleted=true" : "")).then(function (response) {
                if (!alive || ticket !== detailTicket) {
                    return;
                }
                if (!response.ok) {
                    fail(response);
                    return;
                }
                self.detail(response.data);
                if (!response.data.conversationId || deleted) {
                    return;
                }
                return api.get("/reports/" + encodeURIComponent(item.id) + "/conversation").then(function (conversation) {
                    if (!alive || ticket !== detailTicket) {
                        return;
                    }
                    if (conversation.ok) {
                        self.conversation(conversation.data);
                    } else {
                        fail(conversation);
                    }
                });
            }).catch(function () {
                if (alive && ticket === detailTicket) {
                    error();
                }
            });
        };
        self.action = function (action, body) {
            var detail = self.detail();
            if (!detail || !self.allowed() || self.busy() || ((action === "restore" || action === "hard/delete") && !self.developer())) {
                return;
            }
            if (action === "hard/delete" && !window.confirm(i18n.t("reports.confirmHardDelete"))) {
                return;
            }
            self.busy(true);
            self.message("");
            var ticket = detailTicket;
            return api.post("/reports/" + encodeURIComponent(detail.id) + "/" + action, body).then(function (response) {
                if (!alive || ticket !== detailTicket) {
                    return;
                }
                if (!response.ok) {
                    fail(response);
                    return;
                }
                if (action === "delete" || action === "restore" || action === "hard/delete") {
                    self.detail(null);
                    self.conversation(null);
                    return self.load();
                }
                self.message(i18n.t("reports.updated"));
                return self.open(detail);
            }).catch(error).finally(function () {
                if (alive) {
                    self.busy(false);
                }
            });
        };
        self.resolve = function (action) {
            var date = self.banUntil();
            var parsed = date ? new Date(date) : null;
            if (parsed && (!Number.isFinite(parsed.getTime()) || parsed <= new Date())) {
                self.message(i18n.t("reports.invalidBanDate"));
                return;
            }
            return self.action("resolve", { action: action, banUntil: parsed ? parsed.toISOString() : null, reporterMessage: self.reporterMessage().trim() || null, reportedEmail: self.reportedEmail().trim() || null });
        };
        self.activate = self.load;
        self.dispose = function () {
            alive = false;
            listTicket++; detailTicket++;
            identitySubscription.dispose();
            self.allowed.dispose(); self.developer.dispose();
        };
    };
});
