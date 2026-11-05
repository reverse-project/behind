define(["ko", "models/auth", "models/webauthn"], function (ko, auth, webauthn) {
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
        self.enrollment = ko.observable(null);
        self.enrollmentCode = ko.observable("");
        self.passkeyName = ko.observable("");
        self.recoveryCodes = ko.observableArray([]);
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

        self.passkeyAvailable = ko.pureComputed(function () {
            return has("Passkey") && webauthn.supported();
        });

        self.usingRecovery = ko.pureComputed(function () {
            return self.hasRecovery() && (self.recoveryChosen() || !self.hasTotp());
        });

        self.showTotpForm = ko.pureComputed(function () {
            return !self.setupRequired() && self.hasTotp() && !self.usingRecovery();
        });

        self.showRecoveryForm = ko.pureComputed(function () {
            return !self.setupRequired() && self.usingRecovery();
        });

        self.canSwitchCode = ko.pureComputed(function () {
            return !self.setupRequired() && self.hasTotp() && self.hasRecovery();
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

            options.done(signedIn);
        };

        self.proceed = function () {
            var result = finished;

            options.done(result);
            self.recoveryCodes([]);
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
            return run(function () {
                return auth.twoFactor.verifyTotp(self.ticket(), compact(self.code));
            }, self.complete);
        };

        self.verifyRecovery = function () {
            return run(function () {
                return auth.twoFactor.verifyRecovery(self.ticket(), compact(self.recoveryCode));
            }, self.complete);
        };

        self.usePasskey = function () {
            return run(function () {
                return auth.twoFactor.signInWithPasskey(self.ticket());
            }, self.complete);
        };

        self.startTotp = function () {
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
            return run(function () {
                return auth.twoFactor.setupPasskey(self.ticket(), self.passkeyName());
            }, self.complete);
        };

        self.toggleRecovery = function () {
            self.message("");
            self.recoveryChosen(!self.recoveryChosen());
        };

        self.back = function () {
            restart("");
        };
    };
});
