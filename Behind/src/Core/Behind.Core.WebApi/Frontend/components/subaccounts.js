define(["ko", "models/http", "models/session", "models/invalidation", "models/i18n", "models/user", "models/accounts", "text!components/subaccounts.html"], function (ko, http, session, invalidation, i18n, user, modes, template) {
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
        var currentIdentity = function () {
            return session.accessToken() && !session.isImpersonating() ? session.generation() : null;
        };
        var identity = ko.pureComputed(currentIdentity);

        self.managementAccounts = ko.pureComputed(function () {
            return self.primary() ? [{ userId: self.primaryUserId(), isPrimary: true, hasPin: self.primaryHasPin() }].concat(self.accounts()) : [];
        });
        self.proofMethods = ko.pureComputed(function () {
            var account = self.selected();
            var methods = [];
            if (account ? account.hasPin : self.primaryHasPin()) {
                methods.push("currentPin");
            }
            if (account && !account.isPrimary && self.primary() && self.primaryHasPin()) {
                methods.push("primaryPin");
            }
            if (modes.password()) {
                methods.push("password");
            }
            if (modes.magicLink()) {
                methods.push("magicToken");
            }
            return methods;
        });
        self.proofLabel = function (method) {
            var account = self.selected();
            var labels = {
                currentPin: account && !account.isPrimary ? "subaccount.subaccountCurrentPin" : "subaccount.yourCurrentPin",
                primaryPin: "subaccount.yourCurrentPin",
                password: "subaccount.yourPassword",
                magicToken: "subaccount.magicProof"
            };
            return i18n.t(labels[method]);
        };
        self.proofMethod = ko.observable("password");
        self.credential = ko.observable("");
        var methodChange = self.proofMethod.subscribe(function () { self.credential(""); });
        var clearProof = function () {
            self.proofMethod(self.proofMethods()[0]);
            self.credential("");
        };
        var operationChange = self.clearPin.subscribe(clearProof);
        self.needsProof = ko.pureComputed(function () {
            return !!(self.pin() || self.clearPin() || (!self.selected() && !self.primaryHasPin() && !self.skipPrimaryPin() && self.primaryPin()));
        });
        var select = self.selected.subscribe(function (account) {
            self.pin("");
            self.primaryPin("");
            self.skipPrimaryPin(false);
            self.name(account && account.displayName || "");
            clearProof();
            self.child(!!account && account.isChild);
            self.birthday(account && account.birthday || "");
            self.email("");
            self.clearPin(false);
            self.message("");
        });

        self.label = function (account) {
            if (account.isPrimary) {
                return i18n.t("subaccount.primaryLabel");
            }
            var label = user.label({ id: account.userId, displayName: account.displayName });
            return account.userId === self.primaryUserId() ? label + " (" + i18n.t("subaccount.primaryLabel") + ")" : label;
        };

        var load = function () {
            var ticket = ++revision;
            self.busy(false);
            clearProof();
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
            if (identity() === null) {
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
        var identityChanges = identity.subscribe(load);
        var changes = invalidation.subscribe("subaccounts", load);
        self.dispose = function () {
            revision++;
            changes.dispose();
            identityChanges.dispose();
            select.dispose();
            methodChange.dispose();
            operationChange.dispose();
            self.needsProof.dispose();
            self.proofMethods.dispose();
            self.managementAccounts.dispose();
            self.credential("");
            identity.dispose();
        };

        var perform = function (operation, done, invalid, acceptSuccess) {
            if (self.busy()) {
                return;
            }
            self.busy(true);
            self.message("");
            var ticket = revision;
            return operation().then(function (response) {
                if (response.ok) {
                    if (acceptSuccess ? acceptSuccess() : ticket === revision) {
                        return done(response.data);
                    }
                } else if (ticket === revision) {
                    self.message(i18n.t(response.status === 429 ? "auth.tooManyAttempts" : typeof invalid === "function" ? invalid(response.status) : invalid));
                }
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
                var accountSession = identity();
                return Promise.resolve(invalidation.invalidate("subaccounts")).then(function () {
                    if (identity() === accountSession) {
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

        self.requestPinProof = function () {
            var account = self.selected();
            var operation = account ? (self.clearPin() ? "remove" : "change") : "create";
            return perform(function () {
                return http.post(endpoint(account ? account.userId : self.primaryUserId()) + "/pin-proof", { operation: operation });
            }, function () {
                self.message(i18n.t("subaccount.mailInstructions"));
            }, "subaccount.denied");
        };

        self.save = function () {
            var account = self.selected();
            var body = {
                displayName: self.name().trim(),
                isChild: self.child(),
                birthday: self.birthday() || null,
                pin: self.clearPin() ? null : self.pin() || null
            };
            if (self.needsProof()) {
                if (self.proofMethods().indexOf(self.proofMethod()) < 0 || !self.credential()) {
                    self.message(i18n.t("subaccount.proofRequired"));
                    return;
                }
                body.proof = {};
                body.proof[self.proofMethod()] = self.credential();
            }
            return perform(function () {
                if (account && account.isPrimary) {
                    return http.put(endpoint(account.userId) + "/pin", { pin: body.pin, clearPin: self.clearPin(), proof: body.proof });
                }
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
            }, function (status) {
                return status === 403 && body.proof ? "subaccount.proofRejected" : "subaccount.invalid";
            });
        };

        self.switchUser = function () {
            var account = self.selected();
            var sourceIdentity = currentIdentity();
            if (!account || sourceIdentity === null) {
                return;
            }
            return perform(function () {
                return http.post(endpoint(account.userId) + "/switch", { pin: self.pin() || null });
            }, function (tokens) {
                session.start(tokens);
            }, "subaccount.denied", function () {
                return currentIdentity() === sourceIdentity;
            });
        };

        load();
    };

    return { viewModel: Panel, template: template };
});
