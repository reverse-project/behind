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
                } else if (self.manager) {
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
                self.message(i18n.t("common.error.generic"));
            }).finally(function () {
                self.busy(false);
            });
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
