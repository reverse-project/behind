define(["ko", "models/auth", "models/i18n"], function (ko, auth, i18n) {
    return function MagicLinkViewModel(context) {
        var self = this;

        self.message = ko.observable(i18n.t("magiclink.signingIn"));
        self.failed = ko.observable(false);

        self.activate = function () {
            context.scrub("#/sign/in/magiclink");

            return auth.signInWithMagicLink(context.params.token).then(function (result) {
                if (result.ok) {
                    context.replace("#/");
                    return;
                }

                self.message(result.message);
                self.failed(true);
            });
        };
    };
});
