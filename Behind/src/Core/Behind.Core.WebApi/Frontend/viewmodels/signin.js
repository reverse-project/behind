define(["ko", "viewmodels/captchaform", "viewmodels/twofactorchallenge", "models/auth", "models/accounts", "models/i18n"], function (ko, CaptchaFormViewModel, TwoFactorChallengeViewModel, auth, accounts, i18n) {
    return function SignInViewModel(context) {
        var self = this;
        CaptchaFormViewModel.call(self);

        self.passwordSignInEnabled = accounts.password;
        self.magicLinkSignInEnabled = accounts.magicLink;
        self.passkeySignInEnabled = accounts.passkey;
        self.oidcProviders = ko.observableArray([]);
        self.oidcSignInLabel = function (displayName) {
            return i18n.t("signin.oidc.provider", [displayName]);
        };

        self.twoFactor = new TwoFactorChallengeViewModel({
            done: function (result) {
                context.navigate(result.lostPassword ? "#/profile/password" : "#/");
            },
            back: function (message) {
                self.message(message);
            }
        });

        var signIn = function (action) {
            return self.run(action).then(function (result) {
                if (result.twoFactor) {
                    self.twoFactor.begin(result.twoFactor);
                    return result;
                }

                if (result.ok) {
                    self.twoFactor.complete(result);
                }
                return result;
            });
        };

        var loadOidcProviders = function () {
            return auth.listOidcProviders().then(function (result) {
                if (result.ok) {
                    self.oidcProviders(result.providers);

                    if (result.providers.length > 0 && window.location.protocol !== "https:") {
                        self.oidcProviders([]);
                        self.message(i18n.t("signin.oidc.error"));
                    }

                    return;
                }

                if (!self.message()) {
                    self.message(result.message);
                }
            });
        };

        self.submit = function () {
            if (!accounts.password() || self.busy()) {
                return Promise.resolve();
            }

            return signIn(function (captcha) {
                return auth.signIn(self.email(), self.password(), captcha);
            });
        };

        self.requestLegacyPasskeyLink = function (model, event) {
            if (self.busy() || !event.currentTarget.form.reportValidity()) {
                return Promise.resolve();
            }

            return self.run(function (captcha) {
                return auth.requestLegacyPasskeyLink(self.email(), captcha);
            });
        };

        var legacyTicket = null;
        self.legacyLinkOpened = ko.observable(false);
        self.useLegacyPasskey = function () {
            if (!legacyTicket || self.busy()) {
                return Promise.resolve();
            }

            var ticket = legacyTicket;
            legacyTicket = null;
            self.legacyLinkOpened(false);
            return signIn(function () {
                return auth.signInWithPasskey(null, null, ticket);
            });
        };

        self.usePasskey = function (model, event) {
            if (!accounts.passkey() || self.busy()) {
                return Promise.resolve();
            }

            var form = event && event.currentTarget && event.currentTarget.form;

            if (form && !form.reportValidity()) {
                return Promise.resolve();
            }

            return signIn(function (captcha) {
                return auth.signInWithPasskey(self.email(), captcha);
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

        self.signInWithOidc = function (provider) {
            if (!provider || !provider.id || self.busy()) {
                return;
            }

            var url = auth.oidcUrl(provider.id);

            if (window.location.protocol !== "https:" || new URL(url).protocol !== "https:") {
                self.message(i18n.t("signin.oidc.error"));
                return;
            }

            window.location.assign(url);
        };

        var activate = self.activate;
        self.activate = function () {
            activate();

            var params = context.params || {};

            if (params.legacyPasskey) {
                legacyTicket = params.legacyPasskey;
                context.scrub("#/sign-in");
                delete params.legacyPasskey;
                self.legacyLinkOpened(true);
                return;
            }

            if (params.oidcError) {
                context.scrub("#/sign-in");
                delete params.oidc;
                delete params.oidcError;
                self.message(i18n.t("signin.oidc.error"));
                return loadOidcProviders();
            }

            if (params.oidc) {
                var ticket = params.oidc;
                context.scrub("#/sign-in");
                delete params.oidc;

                if (window.location.protocol !== "https:") {
                    self.message(i18n.t("signin.oidc.error"));
                    return loadOidcProviders();
                }

                return signIn(function () {
                    return auth.redeemOidc(ticket);
                }).then(function (result) {
                    return result && (result.ok || result.twoFactor) ? undefined : loadOidcProviders();
                });
            }

            return loadOidcProviders();
        };
    };
});
