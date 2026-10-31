define(["viewmodels/form", "models/auth"], function (FormViewModel, auth) {
    return function SignUpViewModel() {
        var self = this;
        FormViewModel.call(self);

        self.submit = function () {
            return self.run(function () {
                return auth.signUp(self.email(), self.password());
            });
        };
    };
});
