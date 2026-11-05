define(["ko", "viewmodels/captchaform", "models/auth"], function (ko, CaptchaFormViewModel, auth) {
    return function SignUpViewModel() {
        var self = this;
        CaptchaFormViewModel.call(self);

        self.displayName = ko.observable("");

        self.submit = function () {
            return self.run(function (captcha) {
                return auth.signUp(self.email(), self.password(), self.displayName(), captcha);
            });
        };
    };
});
