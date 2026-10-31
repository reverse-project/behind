define(["ko", "models/auth"], function (ko, auth) {
    return function MagicLinkViewModel(context) {
        var self = this;

        self.message = ko.observable("Signing you in...");
        self.failed = ko.observable(false);

        self.activate = function () {
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
