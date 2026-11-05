define(["viewmodels/captchaform", "viewmodels/twofactorchallenge", "models/auth"], function (CaptchaFormViewModel, TwoFactorChallengeViewModel, auth) {
    return function SignInViewModel(context) {
        var self = this;
        CaptchaFormViewModel.call(self);

        self.twoFactor = new TwoFactorChallengeViewModel({
            done: function (result) {
                context.navigate(result.lostPassword ? "#/profile/password" : "#/");
            },
            back: function (message) {
                self.message(message);
            }
        });

        self.submit = function () {
            return self.run(function (captcha) {
                return auth.signIn(self.email(), self.password(), captcha);
            }).then(function (result) {
                if (result.twoFactor) {
                    self.twoFactor.begin(result.twoFactor);
                    return;
                }

                if (result.ok) {
                    self.twoFactor.complete(result);
                }
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
