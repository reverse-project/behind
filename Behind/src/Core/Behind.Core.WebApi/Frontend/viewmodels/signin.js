define(["viewmodels/form", "models/auth"], function (FormViewModel, auth) {
    return function SignInViewModel(context) {
        var self = this;
        FormViewModel.call(self);

        self.submit = function () {
            return self.run(function () {
                return auth.signIn(self.email(), self.password());
            }).then(function (result) {
                if (result.ok) {
                    context.navigate("#/");
                }
            });
        };

        self.requestMagicLink = function () {
            return self.run(function () {
                return auth.requestMagicLink(self.email());
            });
        };

        self.requestLostPassword = function () {
            return self.run(function () {
                return auth.requestMagicLink(self.email(), "auth.lostPassword.requested", "LostPassword");
            });
        };

        self.resendConfirmation = function () {
            return self.run(function () {
                return auth.resendConfirmation(self.email());
            });
        };
    };
});
