define(["viewmodels/captchaform", "viewmodels/twofactorchallenge", "models/auth", "models/accounts"], function (CaptchaFormViewModel, TwoFactorChallengeViewModel, auth, accounts) {
    return function SignInViewModel(context) {
        var self = this;
        CaptchaFormViewModel.call(self);

        self.passwordSignInEnabled = accounts.password;
        self.magicLinkSignInEnabled = accounts.magicLink;
        self.passkeySignInEnabled = accounts.passkey;

        self.twoFactor = new TwoFactorChallengeViewModel({
            done: function (result) {
                context.navigate(result.lostPassword ? "#/profile/password" : "#/");
            },
            back: function (message) {
                self.message(message);
            }
        });

        var signIn = function (action) {
            return self.run(action).then(function (result) {
                if (result.twoFactor) {
                    self.twoFactor.begin(result.twoFactor);
                    return;
                }

                if (result.ok) {
                    self.twoFactor.complete(result);
                }
            });
        };

        self.submit = function () {
            if (!accounts.password() || self.busy()) {
                return Promise.resolve();
            }

            return signIn(function (captcha) {
                return auth.signIn(self.email(), self.password(), captcha);
            });
        };

        self.usePasskey = function (model, event) {
            if (!accounts.passkey() || self.busy()) {
                return Promise.resolve();
            }

            var form = event && event.currentTarget && event.currentTarget.form;

            if (form && !form.reportValidity()) {
                return Promise.resolve();
            }

            return signIn(function (captcha) {
                return auth.signInWithPasskey(self.email(), captcha);
            });
        };

        self.requestMagicLink = function () {
            return self.run(function (captcha) {
                return auth.requestMagicLink(self.email(), captcha);
            });
        };

        self.requestLostPassword = function () {
            return self.run(function (captcha) {
                return auth.requestMagicLink(self.email(), captcha, "auth.lostPassword.requested", "LostPassword");
            });
        };

        self.resendConfirmation = function () {
            return self.run(function (captcha) {
                return auth.resendConfirmation(self.email(), captcha);
            });
        };
    };
});
