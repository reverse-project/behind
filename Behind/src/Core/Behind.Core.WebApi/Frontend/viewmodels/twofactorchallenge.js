define(["ko", "models/auth", "models/webauthn", "models/accounts", "models/i18n"], function (ko, auth, webauthn, accounts, i18n) {
    var compact = function (value) {
        return (value() || "").replace(/\s+/g, "");
    };

    return function TwoFactorChallengeViewModel(options) {
        var self = this;
        var finished = null;

        self.stage = ko.observable("idle");
        self.challenge = ko.observable(null);
        self.ticket = ko.observable("");
        self.code = ko.observable("");
        self.recoveryCode = ko.observable("");
        self.recoveryChosen = ko.observable(false);
        self.emailChosen = ko.observable(false);
        self.emailSent = ko.observable(false);
        self.emailCode = ko.observable("");
        self.emailAddresses = ko.observableArray([]);
        self.emailId = ko.observable("");
        self.emailId.subscribe(function () {
            self.emailSent(false);
            self.emailCode("");
        });
        self.enrollment = ko.observable(null);
        self.enrollmentCode = ko.observable("");
        self.passkeyName = ko.observable("");
        self.recoveryCodes = ko.observableArray([]);
        self.canSetupTotp = accounts.twoFactor;
        self.canSetupPasskey = ko.pureComputed(function () {
            return accounts.twoFactor() && accounts.passkey() && self.passkeyAvailable();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");

        self.active = ko.pureComputed(function () {
            return self.stage() !== "idle";
        });

        self.showChallenge = ko.pureComputed(function () {
            return self.stage() === "challenge";
        });

        self.showCodes = ko.pureComputed(function () {
            return self.stage() === "codes";
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        var has = function (method) {
            var current = self.challenge();

            return !!current && current.methods.indexOf(method) >= 0;
        };

        self.setupRequired = ko.pureComputed(function () {
            var current = self.challenge();

            return !!current && current.setupRequired;
        });

        self.hasTotp = ko.pureComputed(function () {
            return has("Totp");
        });

        self.hasRecovery = ko.pureComputed(function () {
            return has("Recovery");
        });

        self.hasEmail = ko.pureComputed(function () {
            return options.allowEmail !== false && has("Email");
        });

        self.hasEmailAddresses = ko.pureComputed(function () {
            return self.emailAddresses().length > 0;
        });

        self.chooseEmailLabel = ko.pureComputed(function () {
            return i18n.t("twoFactor.challenge.chooseEmail");
        });

        self.passkeyAvailable = ko.pureComputed(function () {
            return has("Passkey") && webauthn.supported();
        });

        self.usingRecovery = ko.pureComputed(function () {
            return self.hasRecovery() && (self.recoveryChosen() || (!self.hasTotp() && !self.hasEmail()));
        });

        self.showTotpForm = ko.pureComputed(function () {
            return !self.setupRequired() && self.hasTotp() && !self.usingRecovery() && !self.emailChosen();
        });

        self.showEmailForm = ko.pureComputed(function () {
            return self.hasEmail() && !self.usingRecovery()
                && (self.emailChosen() || (!self.setupRequired() && !self.hasTotp()));
        });

        self.showRecoveryForm = ko.pureComputed(function () {
            return !self.setupRequired() && self.usingRecovery();
        });

        self.canSwitchCode = ko.pureComputed(function () {
            return !self.setupRequired() && (self.hasTotp() || self.hasEmail()) && self.hasRecovery();
        });

        self.hasEnrollment = ko.pureComputed(function () {
            return self.enrollment() !== null;
        });

        var reset = function () {
            finished = null;
            self.stage("idle");
            self.challenge(null);
            self.ticket("");
            self.code("");
            self.recoveryCode("");
            self.recoveryChosen(false);
            self.emailChosen(false);
            self.emailSent(false);
            self.emailCode("");
            self.emailAddresses([]);
            self.emailId("");
            self.enrollment(null);
            self.enrollmentCode("");
            self.passkeyName("");
            self.recoveryCodes([]);
            self.message("");
        };

        var restart = function (message) {
            reset();
            options.back(message);
        };

        self.begin = function (challenge) {
            reset();
            self.challenge(challenge);
            self.ticket(challenge.ticket);
            self.stage("challenge");

            if (self.showEmailForm()) {
                return self.loadEmailAddresses();
            }
        };

        self.complete = function (result) {
            var challenge = self.challenge();
            var signedIn = Object.assign({}, result, {
                lostPassword: result.lostPassword || (!!challenge && challenge.function === "LostPassword")
            });

            if (signedIn.recoveryCodes && signedIn.recoveryCodes.length > 0) {
                finished = signedIn;
                self.recoveryCodes(signedIn.recoveryCodes);
                self.stage("codes");
                return;
            }

            reset();
            options.done(signedIn);
        };

        self.proceed = function () {
            var result = finished;

            reset();
            options.done(result);
        };

        var run = function (action, onOk) {
            self.busy(true);
            self.message("");

            return action()
                .then(function (result) {
                    if (result.ticket) {
                        self.ticket(result.ticket);
                    }

                    if (result.restart) {
                        return restart(result.message);
                    }

                    if (!result.ok) {
                        self.code("");
                        self.recoveryCode("");
                        self.enrollmentCode("");
                        self.emailCode("");
                        self.message(result.message);
                        return undefined;
                    }

                    return onOk(result);
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.verifyTotp = function () {
            if (!self.showTotpForm() || self.busy()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.verifyTotp(self.ticket(), compact(self.code));
            }, self.complete);
        };

        self.verifyRecovery = function () {
            if (!self.showRecoveryForm() || self.busy()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.verifyRecovery(self.ticket(), compact(self.recoveryCode));
            }, self.complete);
        };

        self.usePasskey = function () {
            if (self.setupRequired() || !self.passkeyAvailable() || self.busy()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.signInWithPasskey(self.ticket());
            }, self.complete);
        };

        self.loadEmailAddresses = function () {
            if (!self.showEmailForm() || self.busy()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.emailAddresses(self.ticket());
            }, function (result) {
                self.emailAddresses(result.data.addresses || []);
            });
        };

        self.sendEmail = function () {
            var emailId = self.emailId();
            var available = self.emailAddresses().some(function (email) {
                return email.id === emailId;
            });

            if (!self.showEmailForm() || !available || self.busy()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.sendEmail(self.ticket(), emailId);
            }, function (result) {
                self.emailSent(true);
                self.emailCode("");
                self.message(result.message);
            });
        };

        self.verifyEmail = function () {
            if (!self.showEmailForm() || !self.emailSent() || self.busy()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.verifyEmail(self.ticket(), compact(self.emailCode));
            }, self.complete);
        };

        self.startTotp = function () {
            if (!self.canSetupTotp()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.setupTotp(self.ticket());
            }, function (result) {
                self.enrollment(result.data);
            });
        };

        self.confirmTotp = function () {
            return run(function () {
                return auth.twoFactor.confirmSetupTotp(self.ticket(), compact(self.enrollmentCode));
            }, self.complete);
        };

        self.registerPasskey = function () {
            if (!self.canSetupPasskey()) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.setupPasskey(self.ticket(), self.passkeyName());
            }, self.complete);
        };

        self.toggleRecovery = function () {
            if (!self.canSwitchCode() || self.busy()) {
                return;
            }

            self.message("");
            self.recoveryChosen(!self.recoveryChosen());
        };

        self.useEmail = function () {
            if (!self.hasEmail() || self.busy()) {
                return Promise.resolve();
            }

            self.message("");
            self.recoveryChosen(false);
            self.emailChosen(true);

            if (!self.hasEmailAddresses()) {
                return self.loadEmailAddresses();
            }

            return Promise.resolve();
        };

        self.useTotp = function () {
            if (self.setupRequired() || !self.hasTotp() || self.busy()) {
                return;
            }

            self.message("");
            self.recoveryChosen(false);
            self.emailChosen(false);
        };

        self.back = function () {
            if (self.busy()) {
                return;
            }

            restart("");
        };
    };
});
