define(["ko", "viewmodels/twofactorchallenge", "models/auth", "models/i18n"], function (ko, TwoFactorChallengeViewModel, auth, i18n) {
    return function MagicLinkViewModel(context) {
        var self = this;
        var resetToken = null;

        self.message = ko.observable(i18n.t("magiclink.signingIn"));
        self.failed = ko.observable(false);
        self.emailNotConfirmed = ko.observable(false);
        self.resettingPassword = ko.observable(false);
        self.newPassword = ko.observable("");
        self.resetMessage = ko.observable("");
        self.passwordChanged = ko.observable(false);
        self.busy = ko.observable(false);

        var beginPasswordReset = function (grant) {
            resetToken = grant.token;
            self.message("");
            self.resettingPassword(true);
        };

        var finish = function (result) {
            if (result.passwordReset) {
                beginPasswordReset(result.passwordReset);
                return;
            }

            context.replace(result.lostPassword ? "#/profile/password" : "#/");
        };

        self.twoFactor = new TwoFactorChallengeViewModel({
            allowEmail: false,
            done: finish,
            back: function (restartMessage) {
                if (!restartMessage) {
                    context.navigate("#/sign-in");
                    return;
                }

                self.message(restartMessage);
                self.failed(true);
            }
        });

        self.submitPassword = function () {
            self.busy(true);
            self.resetMessage("");

            return auth.resetPassword(resetToken, self.newPassword()).then(function (result) {
                if (result.ok) {
                    resetToken = null;
                    self.newPassword("");
                    self.resettingPassword(false);
                    self.message(result.message);
                    self.passwordChanged(true);
                    return;
                }

                self.resetMessage(result.message);
            }).finally(function () {
                self.busy(false);
            });
        };

        self.activate = function () {
            context.scrub("#/sign/in/magiclink");

            return auth.signInWithMagicLink(context.params.token).then(function (result) {
                if (result.twoFactor) {
                    self.twoFactor.begin(result.twoFactor);
                    return;
                }

                if (result.ok) {
                    self.twoFactor.complete(result);
                    return;
                }

                self.message(result.message);
                self.failed(true);
                self.emailNotConfirmed(!!result.emailNotConfirmed);
            });
        };
    };
});
