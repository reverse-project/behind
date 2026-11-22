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
        self.profileType = ko.observable();
        self.profileTypes = ko.pureComputed(function () {
            return ["adult", "child"].filter(function (type) {
                return self.accounts().some(function (account) { return !!account.isChild === (type === "child"); });
            });
        });
        self.switchProfiles = ko.pureComputed(function () {
            return self.accounts().filter(function (account) {
                return !!account.isChild === (self.profileType() === "child");
            });
        });
        self.profileTypeLabel = function (type) {
            return i18n.t("subaccount.profileType." + type);
        };
        var profileTypeChange = self.profileType.subscribe(function () { self.selected(undefined); });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.pin = ko.observable("");
        self.name = ko.observable("");
        self.email = ko.observable("");
        self.child = ko.observable(false);
        self.birthday = ko.observable("");
        self.clearPin = ko.observable(false);
        self.canBrowseAndRelease = ko.observable(false);
        self.linkEnabled = ko.observable(false);
        var canRespondToInvitations = ko.observable(false);
        self.linkRequiresDirectSignIn = ko.observable(false);
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

        var profileIdentity = identity();
        var currentHasPin = ko.observable(null);
        self.editAccount = ko.pureComputed(function () {
            if (!self.manager || self.primary()) {
                return self.selected();
            }
            var id = ko.unwrap(params.userId);
            return id && self.linked() && currentHasPin() !== null && identity() === profileIdentity
                ? { userId: id, hasPin: currentHasPin() } : null;
        });
        self.pinOnly = ko.pureComputed(function () {
            var account = self.editAccount();
            return account && (!self.primary() || account.isPrimary);
        });
        self.managementAccounts = ko.pureComputed(function () {
            return self.primary() ? [{ userId: self.primaryUserId(), isPrimary: true, hasPin: self.primaryHasPin() }].concat(self.accounts()) : [];
        });
        self.proofMethods = ko.pureComputed(function () {
            var account = self.editAccount();
            if (!self.primary()) {
                return self.manager && account && account.hasPin ? ["currentPin"] : [];
            }
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
                currentPin: self.primary() && account && !account.isPrimary ? "subaccount.subaccountCurrentPin" : "subaccount.yourCurrentPin",
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
        var operationChange = self.clearPin.subscribe(function () {
            if (self.primary()) {
                clearProof();
            }
        });
        self.changingPin = ko.pureComputed(function () {
            return !!(self.pin() || self.clearPin() || (self.primary() && !self.selected() && !self.primaryHasPin() && !self.skipPrimaryPin() && self.primaryPin()));
        });
        self.needsProof = ko.pureComputed(function () {
            var account = self.editAccount();
            return self.changingPin() && (self.primary() || !!(account && account.hasPin));
        });
        var select = self.editAccount.subscribe(function (account) {
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
            self.canBrowseAndRelease(false);
            self.linkEnabled(false);
            canRespondToInvitations(false);
            self.linkRequiresDirectSignIn(false);
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
            currentHasPin(null);
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
                    self.accounts(self.manager ? response.data.accounts : response.data.switchAccounts ?? response.data.accounts);
                    self.profileType(self.profileTypes()[0]);
                    self.primary(response.data.isPrimary);
                    self.linked(response.data.isLinked ?? (!response.data.isPrimary && !!response.data.primaryUserId));
                    currentHasPin(typeof response.data.currentHasPin === "boolean" ? response.data.currentHasPin : null);
                    clearProof();
                    self.linkEnabled(!!response.data.linkUnlinkEnabled);
                    canRespondToInvitations(response.data.canRespondToInvitations ?? (self.linkEnabled() && self.primary()));
                    self.canBrowseAndRelease(response.data.canBrowseAndRelease ?? self.linkEnabled());
                    self.linkRequiresDirectSignIn(response.data.linkUnlinkRequiresDirectSignIn === true);
                    if (self.manager && canRespondToInvitations()) {
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
            profileTypeChange.dispose();
            self.profileTypes.dispose();
            self.switchProfiles.dispose();
            methodChange.dispose();
            operationChange.dispose();
            self.needsProof.dispose();
            self.changingPin.dispose();
            self.proofMethods.dispose();
            self.managementAccounts.dispose();
            self.pinOnly.dispose();
            self.editAccount.dispose();
            self.credential("");
            self.pin("");
            self.primaryPin("");
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
                    if (response.status !== 429 && typeof invalid === "function") {
                        return invalid(response.status);
                    }
                    self.message(i18n.t(response.status === 429 ? "auth.tooManyAttempts" : invalid));
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
            if (!self.canBrowseAndRelease() || self.busy()) {
                return;
            }
            self.candidates([]);
            self.moreCandidates(false);
            return findCandidates(1, self.query().trim());
        };

        self.loadMore = function () {
            if (self.canBrowseAndRelease() && self.moreCandidates()) {
                return findCandidates(candidatePage + 1, searchedName);
            }
        };

        var refresh = function (success) {
            var accountSession = currentIdentity();
            return invalidation.invalidate("subaccounts").then(function () {
                if (success && currentIdentity() === accountSession && !self.message()) {
                    self.message(i18n.t(success));
                }
            });
        };

        var changeLink = function (operation, success) {
            return perform(operation, function () {
                return refresh(success);
            }, "subaccount.linkFailed");
        };

        self.requestLink = function (account) {
            if (!self.linkEnabled()) {
                return;
            }
            return changeLink(function () {
                return http.post(endpoint(account.userId) + "/request");
            }, "subaccount.requestSent");
        };

        var answer = function (request, accept) {
            if (!canRespondToInvitations()) {
                return;
            }
            return changeLink(function () {
                return http.post(path + "/requests/" + encodeURIComponent(request.requestId) + (accept ? "/confirm" : "/decline"));
            }, accept ? "subaccount.accepted" : "subaccount.declined");
        };
        self.confirmLink = function (request) { return answer(request, true); };
        self.declineLink = function (request) { return answer(request, false); };

        self.release = function () {
            var account = self.selected();
            if (!self.canBrowseAndRelease() || !account || !account.canRelease || self.busy()) {
                return;
            }
            var switched = self.linkRequiresDirectSignIn();
            var confirmation = i18n.t("subaccount.confirmRelease") + " " + self.label(account);
            if (switched) {
                confirmation += "\n\n" + i18n.t("subaccount.releaseEndsSession");
            }
            if (!window.confirm(confirmation)) {
                return;
            }
            var owner = currentIdentity();
            return perform(function () {
                return http.delete(endpoint(account.userId));
            }, function () {
                if (switched) {
                    session.end();
                    window.location.hash = "#/sign-in";
                    return;
                }
                return refresh("subaccount.released");
            }, "subaccount.linkFailed", switched ? function () { return currentIdentity() === owner; } : null);
        };

        self.requestPinProof = function () {
            if (!self.manager || !self.primary()) {
                return;
            }
            var account = self.selected();
            var operation = account ? (self.clearPin() ? "remove" : "change") : "create";
            return perform(function () {
                return http.post(endpoint(account ? account.userId : self.primaryUserId()) + "/pin-proof", { operation: operation });
            }, function () {
                self.message(i18n.t("subaccount.mailInstructions"));
            }, "subaccount.denied");
        };

        self.save = function () {
            var account = self.editAccount();
            var settingOwnPin = !self.primary() && account && !account.hasPin;
            if (!self.manager || (!self.primary() && !account) || (self.pinOnly() && !self.changingPin()) || (settingOwnPin && self.clearPin())) {
                return;
            }
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
                if (self.pinOnly()) {
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
                var success = settingOwnPin ? "subaccount.ownPinSet"
                    : body.pin === null ? "subaccount.ownPinRemoved" : "subaccount.ownPinChanged";
                return refresh(self.primary() ? null : success);
            }, function (status) {
                if (settingOwnPin && (status === 403 || status === 409)) {
                    return refresh("subaccount.ownPinSetupRejected");
                }
                var invalid = status === 403 && body.proof
                    ? (self.primary() ? "subaccount.proofRejected" : "subaccount.currentPinRejected")
                    : "subaccount.invalid";
                self.message(i18n.t(invalid));
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
