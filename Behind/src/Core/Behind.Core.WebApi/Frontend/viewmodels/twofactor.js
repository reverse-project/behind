define(["ko", "models/auth", "models/session", "models/webauthn", "models/i18n"], function (ko, auth, session, webauthn, i18n) {
    var presentPasskey = function (item) {
        return {
            id: item.id,
            name: item.name,
            createdAt: i18n.dateTime(item.createdAt)
        };
    };

    return function TwoFactorViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.passkeySupported = webauthn.supported();
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.required = ko.observable(false);
        self.enabled = ko.observable(false);
        self.totpEnabled = ko.observable(false);
        self.totpPending = ko.observable(false);
        self.passkeys = ko.observableArray([]);
        self.recoveryCodesRemaining = ko.observable(0);
        self.enrollment = ko.observable(null);
        self.enrollmentCode = ko.observable("");
        self.passkeyName = ko.observable("");
        self.recoveryCodes = ko.observableArray([]);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.hasEnrollment = ko.pureComputed(function () {
            return self.enrollment() !== null;
        });

        self.hasCodes = ko.pureComputed(function () {
            return self.recoveryCodes().length > 0;
        });

        self.hasPasskeys = ko.pureComputed(function () {
            return self.passkeys().length > 0;
        });

        self.totpStatus = ko.pureComputed(function () {
            if (self.totpEnabled()) {
                return "twoFactor.status.enabled";
            }

            return self.totpPending() ? "twoFactor.status.pending" : "twoFactor.status.disabled";
        });

        self.recoveryRemaining = ko.pureComputed(function () {
            return i18n.t("twoFactor.recovery.remaining", [self.recoveryCodesRemaining()]);
        });

        var show = function (codes) {
            self.recoveryCodes(codes || []);
        };

        var refresh = function () {
            return auth.twoFactor.load().then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return;
                }

                self.required(result.data.required === true);
                self.enabled(result.data.enabled === true);
                self.totpEnabled(result.data.totpEnabled === true);
                self.totpPending(result.data.totpPending === true);
                self.passkeys((result.data.passkeys || []).map(presentPasskey));
                self.recoveryCodesRemaining(result.data.recoveryCodesRemaining || 0);
            });
        };

        var run = function (action, onOk) {
            self.busy(true);

            return action()
                .then(function (result) {
                    self.message(result.message);

                    return result.ok ? onOk(result) : undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        var confirmed = function (key, args) {
            return window.confirm(i18n.t(key, args));
        };

        self.startTotp = function () {
            return run(auth.twoFactor.startTotp, function (result) {
                self.enrollmentCode("");
                self.enrollment(result.data);
            });
        };

        self.confirmTotp = function () {
            return run(function () {
                return auth.twoFactor.confirmTotp(self.enrollmentCode().replace(/\s+/g, ""));
            }, function (result) {
                self.enrollment(null);
                self.enrollmentCode("");
                show(result.recoveryCodes);

                return refresh();
            });
        };

        self.removeTotp = function () {
            if (!confirmed("twoFactor.totp.confirmRemove")) {
                return Promise.resolve();
            }

            return run(auth.twoFactor.removeTotp, function () {
                self.enrollment(null);

                return refresh();
            });
        };

        self.addPasskey = function () {
            return run(function () {
                return auth.twoFactor.addPasskey(self.passkeyName());
            }, function (result) {
                self.passkeyName("");
                show(result.recoveryCodes);

                return refresh();
            });
        };

        self.removePasskey = function (passkey) {
            if (!confirmed("twoFactor.passkeys.confirmRemove", [passkey.name])) {
                return Promise.resolve();
            }

            return run(function () {
                return auth.twoFactor.removePasskey(passkey.id);
            }, refresh);
        };

        self.regenerateRecoveryCodes = function () {
            if (!confirmed("twoFactor.recovery.confirmGenerate")) {
                return Promise.resolve();
            }

            return run(auth.twoFactor.regenerateRecoveryCodes, function (result) {
                show(result.recoveryCodes);

                return refresh();
            });
        };

        self.dismissCodes = function () {
            show([]);
        };

        self.activate = function () {
            if (!session.isSignedIn()) {
                return Promise.resolve();
            }

            self.busy(true);

            return refresh().finally(function () {
                self.busy(false);
            });
        };
    };
});
