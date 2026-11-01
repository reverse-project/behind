define(["ko", "viewmodels/form", "models/auth"], function (ko, FormViewModel, auth) {
    return function SignUpViewModel() {
        var self = this;
        FormViewModel.call(self);

        self.displayName = ko.observable("");

        self.submit = function () {
            return self.run(function () {
                return auth.signUp(self.email(), self.password(), self.displayName());
            });
        };
    };
});
