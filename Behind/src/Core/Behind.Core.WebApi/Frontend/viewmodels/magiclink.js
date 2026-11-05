define(["ko", "viewmodels/twofactorchallenge", "models/auth", "models/i18n"], function (ko, TwoFactorChallengeViewModel, auth, i18n) {
    return function MagicLinkViewModel(context) {
        var self = this;

        self.message = ko.observable(i18n.t("magiclink.signingIn"));
        self.failed = ko.observable(false);
        self.emailNotConfirmed = ko.observable(false);

        self.twoFactor = new TwoFactorChallengeViewModel({
            done: function (result) {
                context.replace(result.lostPassword ? "#/profile/password" : "#/");
            },
            back: function (restartMessage) {
                if (!restartMessage) {
                    context.navigate("#/sign-in");
                    return;
                }

                self.message(restartMessage);
                self.failed(true);
            }
        });

        self.activate = function () {
            context.scrub("#/sign/in/magiclink");

            return auth.signInWithMagicLink(context.params.token).then(function (result) {
                if (result.twoFactor) {
                    self.twoFactor.begin(result.twoFactor);
                    return;
                }

                if (result.ok) {
                    self.twoFactor.complete(result);
                    return;
                }

                self.message(result.message);
                self.failed(true);
                self.emailNotConfirmed(!!result.emailNotConfirmed);
            });
        };
    };
});
