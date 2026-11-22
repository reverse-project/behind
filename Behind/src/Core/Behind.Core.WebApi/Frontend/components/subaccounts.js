define(["ko", "models/http", "models/session", "models/invalidation", "models/i18n", "models/user", "text!components/subaccounts.html"], function (ko, http, session, invalidation, i18n, user, template) {
    var path = "/profile/subaccounts";
    var endpoint = function (id) {
        return path + "/" + encodeURIComponent(id);
    };

    var Panel = function (params) {
        var self = this;
        self.manager = !!params.manager;
        self.translate = i18n.t;
        self.accounts = ko.observableArray([]);
        self.primary = ko.observable(false);
        self.primaryHasPin = ko.observable(false);
        self.primaryUserId = ko.observable();
        self.primaryPin = ko.observable("");
        self.skipPrimaryPin = ko.observable(false);
        self.linked = ko.observable(false);
        self.selected = ko.observable();
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.pin = ko.observable("");
        self.name = ko.observable("");
        self.email = ko.observable("");
        self.child = ko.observable(false);
        self.birthday = ko.observable("");
        self.clearPin = ko.observable(false);
        self.linkEnabled = ko.observable(false);
        self.query = ko.observable("");
        self.candidates = ko.observableArray([]);
        self.moreCandidates = ko.observable(false);
        var candidatePageSize = 10;
        var candidatePage = 0;
        var searchedName = "";
        self.requests = ko.observableArray([]);
        var revision = 0;
        var identity = ko.pureComputed(function () {
            return !session.isImpersonating() && session.accessToken();
        });

        var select = self.selected.subscribe(function (account) {
            self.pin("");
            self.primaryPin("");
            self.skipPrimaryPin(false);
            self.name(account ? account.displayName : "");
            self.child(!!account && account.isChild);
            self.birthday(account && account.birthday || "");
            self.email("");
            self.clearPin(false);
            self.message("");
        });

        self.label = function (account) {
            var label = user.label({ id: account.userId, displayName: account.displayName });
            return account.userId === self.primaryUserId() ? label + " (" + i18n.t("subaccount.primaryLabel") + ")" : label;
        };

        var load = function () {
            var ticket = ++revision;
            self.busy(false);
            self.linkEnabled(false);
            self.query("");
            self.candidates([]);
            self.requests([]);
            self.moreCandidates(false);
            candidatePage = 0;
            searchedName = "";
            self.accounts([]);
            self.selected(null);
            self.name("");
            self.email("");
            self.child(false);
            self.birthday("");
            self.clearPin(false);
            self.pin("");
            self.primaryPin("");
            self.skipPrimaryPin(false);
            self.primary(false);
            self.primaryHasPin(false);
            self.primaryUserId(null);
            self.linked(false);
            self.message("");
            if (!identity()) {
                return Promise.resolve();
            }

            return http.get(path).then(function (response) {
                if (ticket !== revision) {
                    return;
                }
                if (response.ok) {
                    self.primaryUserId(response.data.primaryUserId);
                    self.primaryHasPin(!!response.data.primaryHasPin);
                    self.accounts(response.data.accounts);
                    self.primary(response.data.isPrimary);
                    self.linked(!response.data.isPrimary && !!response.data.primaryUserId);
                    self.linkEnabled(!!response.data.linkUnlinkEnabled);
                    if (self.manager && self.linkEnabled() && self.primary()) {
                        return http.get(path + "/requests").then(function (pending) {
                            if (ticket === revision) {
                                if (pending.ok) {
                                    self.requests(pending.data);
                                } else {
                                    self.message(i18n.t("common.error.generic"));
                                }
                            }
                        });
                    }
                } else if (self.manager && response.status !== 404) {
                    self.message(i18n.t("common.error.generic"));
                }
            }).catch(function () {
                if (ticket === revision && self.manager) {
                    self.message(i18n.t("common.error.generic"));
                }
            });
        };
        var tokenChanges = identity.subscribe(load);
        var changes = invalidation.subscribe("subaccounts", load);
        self.dispose = function () {
            revision++;
            changes.dispose();
            tokenChanges.dispose();
            select.dispose();
            identity.dispose();
        };

        var perform = function (operation, done, invalid) {
            if (self.busy()) {
                return;
            }
            self.busy(true);
            self.message("");
            var ticket = revision;
            return operation().then(function (response) {
                if (ticket !== revision) {
                    return;
                }
                if (response.ok) {
                    return done(response.data);
                }
                self.message(i18n.t(response.status === 429 ? "auth.tooManyAttempts" : invalid));
            }).catch(function () {
                if (ticket === revision) {
                    self.message(i18n.t("common.error.generic"));
                }
            }).finally(function () {
                if (ticket === revision) {
                    self.busy(false);
                }
            });
        };

        var findCandidates = function (page, name) {
            return perform(function () {
                return http.get(path + "/candidates?query=" + encodeURIComponent(name) + "&page=" + page + "&pageSize=" + candidatePageSize);
            }, function (accounts) {
                candidatePage = page;
                searchedName = name;
                self.candidates(page === 1 ? accounts : self.candidates().concat(accounts));
                self.moreCandidates(accounts.length === candidatePageSize);
                if (!self.candidates().length) {
                    self.message(i18n.t("subaccount.noCandidates"));
                }
            }, "subaccount.denied");
        };

        self.search = function () {
            if (!self.linkEnabled() || self.busy()) {
                return;
            }
            self.candidates([]);
            self.moreCandidates(false);
            return findCandidates(1, self.query().trim());
        };

        self.loadMore = function () {
            if (self.linkEnabled() && self.moreCandidates()) {
                return findCandidates(candidatePage + 1, searchedName);
            }
        };

        var changeLink = function (operation, success) {
            if (!self.linkEnabled()) {
                return;
            }
            return perform(operation, function () {
                var accountToken = identity();
                return Promise.resolve(invalidation.invalidate("subaccounts")).then(function () {
                    if (identity() === accountToken) {
                        self.message(i18n.t(success));
                    }
                });
            }, "subaccount.linkFailed");
        };

        self.requestLink = function (account) {
            return changeLink(function () {
                return http.post(endpoint(account.userId) + "/request");
            }, "subaccount.requestSent");
        };

        var answer = function (request, accept) {
            return changeLink(function () {
                return http.post(path + "/requests/" + encodeURIComponent(request.requestId) + (accept ? "/confirm" : "/decline"));
            }, accept ? "subaccount.accepted" : "subaccount.declined");
        };
        self.confirmLink = function (request) { return answer(request, true); };
        self.declineLink = function (request) { return answer(request, false); };

        self.release = function () {
            var account = self.selected();
            if (!self.linkEnabled() || !account || !account.canRelease || !window.confirm(i18n.t("subaccount.confirmRelease") + " " + self.label(account))) {
                return;
            }
            return changeLink(function () {
                return http.delete(endpoint(account.userId));
            }, "subaccount.released");
        };

        self.save = function () {
            var account = self.selected();
            var body = {
                displayName: self.name().trim(),
                isChild: self.child(),
                birthday: self.birthday() || null,
                pin: self.clearPin() ? null : self.pin() || null
            };
            return perform(function () {
                if (account) {
                    body.clearPin = self.clearPin();
                    return http.put(endpoint(account.userId), body);
                }

                if (!self.primaryHasPin() && !self.skipPrimaryPin()) {
                    body.primaryPin = self.primaryPin();
                }
                body.email = self.email().trim() || null;
                return http.post(path, body);
            }, function () {
                return invalidation.invalidate("subaccounts");
            }, "subaccount.invalid");
        };

        self.switchUser = function () {
            var account = self.selected();
            if (!account) {
                return;
            }
            return perform(function () {
                return http.post(endpoint(account.userId) + "/switch", { pin: self.pin() || null });
            }, function (tokens) {
                session.start(tokens);
                window.location.hash = "#/";
                window.location.reload();
            }, "subaccount.denied");
        };

        load();
    };

    return { viewModel: Panel, template: template };
});
