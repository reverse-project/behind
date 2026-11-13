define(["ko", "models/session", "models/privateapi", "models/i18n", "text!components/reportbutton.html"], function (ko, session, api, i18n, template) {
    return {
        viewModel: {
            createViewModel: function (params) {
                var alive = true;
                var serial = 0;
                var identity = session.accessToken();
                var self = {
                    allowed: ko.pureComputed(function () { return session.hasGrant("Report.Write"); }),
                    open: ko.observable(false),
                    busy: ko.observable(false),
                    reason: ko.observable(""),
                    search: ko.observable(""),
                    users: ko.observableArray([]),
                    selected: ko.observableArray((params.userIds || []).slice()),
                    message: ko.observable(""),
                    hasSource: !!params.sourceType,
                    buttonLabel: ko.pureComputed(function () { return i18n.t(params.sourceType ? "reports.report" : "reports.reportUsers"); }),
                    sourceLabel: ko.pureComputed(function () { return params.sourceType ? i18n.t("reports.source." + params.sourceType) : ""; }),
                    t: i18n.t
                };
                var identitySubscription = session.grants.subscribe(function () {
                    if (identity === session.accessToken()) {
                        return;
                    }
                    identity = session.accessToken();
                    serial++;
                    self.open(false); self.reason(""); self.search(""); self.users([]); self.selected([]); self.message("");
                });
                self.find = function () {
                    var ticket = ++serial;
                    return api.get("/private-messages/recipients?" + api.query({ search: self.search(), reportTargets: true })).then(function (response) {
                        if (!alive || ticket !== serial) {
                            return;
                        }
                        if (response.ok) {
                            self.users(response.data.users || []);
                        } else {
                            self.message(api.error(response));
                        }
                    }).catch(function () {
                        if (alive && ticket === serial) {
                            self.message(i18n.t("privateMessages.requestFailed"));
                        }
                    });
                };
                self.submit = function () {
                    if (!self.allowed() || self.busy() || !self.reason().trim() || (!self.hasSource && !self.selected().length)) {
                        return;
                    }
                    self.busy(true);
                    self.message("");
                    var ticket = ++serial;
                    return api.post("/reports", {
                        userIds: self.selected().slice(),
                        reason: self.reason().trim(),
                        sourceType: params.sourceType || null,
                        sourceId: params.sourceType ? ko.unwrap(params.sourceId) : null
                    }).then(function (response) {
                        if (!alive || ticket !== serial) {
                            return;
                        }
                        self.message(response.ok ? i18n.t("reports.submitted") : api.error(response));
                        if (response.ok) {
                            self.reason("");
                            self.open(false);
                        }
                    }).catch(function () {
                        if (alive && ticket === serial) {
                            self.message(i18n.t("privateMessages.requestFailed"));
                        }
                    }).finally(function () {
                        if (alive) {
                            self.busy(false);
                        }
                    });
                };
                self.dispose = function () {
                    alive = false;
                    serial++;
                    identitySubscription.dispose();
                    self.allowed.dispose();
                    self.buttonLabel.dispose();
                    self.sourceLabel.dispose();
                };
                return self;
            }
        },
        template: template
    };
});
