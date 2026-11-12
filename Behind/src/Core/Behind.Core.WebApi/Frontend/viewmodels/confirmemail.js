define(["ko", "models/auth", "models/i18n"], function (ko, auth, i18n) {
    return function ConfirmEmailViewModel(context) {
        var self = this;

        self.message = ko.observable(i18n.t("confirmemail.confirming"));
        self.failed = ko.observable(false);
        self.confirmed = ko.observable(false);

        self.activate = function () {
            context.scrub("#/sign/up/confirm");

            return auth.confirmEmail(context.params.userId, context.params.token, context.params.emailId).then(function (result) {
                self.message(result.message);
                self.confirmed(result.ok);
                self.failed(!result.ok);
            });
        };
    };
});
