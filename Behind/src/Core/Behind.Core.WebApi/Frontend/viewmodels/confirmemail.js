define(["ko", "models/auth"], function (ko, auth) {
    return function ConfirmEmailViewModel(context) {
        var self = this;

        self.message = ko.observable("Confirming your email...");
        self.failed = ko.observable(false);
        self.confirmed = ko.observable(false);

        self.activate = function () {
            context.scrub("#/sign/up/confirm");

            return auth.confirmEmail(context.params.userId, context.params.token).then(function (result) {
                self.message(result.message);
                self.confirmed(result.ok);
                self.failed(!result.ok);
            });
        };
    };
});
