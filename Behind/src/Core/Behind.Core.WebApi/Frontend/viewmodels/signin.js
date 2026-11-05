define(["viewmodels/captchaform", "models/auth"], function (CaptchaFormViewModel, auth) {
    return function SignInViewModel(context) {
        var self = this;
        CaptchaFormViewModel.call(self);

        self.submit = function () {
            return self.run(function (captcha) {
                return auth.signIn(self.email(), self.password(), captcha);
            }).then(function (result) {
                if (result.ok) {
                    context.navigate("#/");
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
