define(["ko", "models/auth", "models/session", "models/user", "models/about", "models/usersettings", "models/i18n"], function (ko, auth, session, user, about, usersettings, i18n) {
    return function ProfileViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.displayName = ko.observable("");
        self.emails = ko.observableArray([]);
        self.locales = ko.observableArray([]);
        self.locale = ko.observable("");

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.shownAs = ko.pureComputed(function () {
            return user.label({ displayName: self.displayName(), emails: self.emails() });
        });

        var load = function () {
            return auth.loadSession().then(function (result) {
                if (result.ok) {
                    self.displayName(result.data.user.displayName);
                    self.emails(result.data.user.emails);
                }

                return result;
            });
        };

        self.submit = function () {
            self.busy(true);

            return auth.changeDisplayName(self.displayName())
                .then(function (result) {
                    return (result.ok ? load() : Promise.resolve()).then(function () {
                        self.message(result.message);
                    });
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.newPassword = ko.observable("");

        self.submitPassword = function () {
            self.busy(true);

            return auth.changePassword(self.newPassword())
                .then(function (result) {
                    if (result.ok) {
                        self.newPassword("");
                    }

                    self.message(result.message);
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        var loadLocales = function () {
            return about.loadLocales().then(function (result) {
                var siteDefault = { code: "", name: i18n.t("profile.siteDefault") };

                self.locales([siteDefault].concat(result.items));
                self.locale(session.isSignedIn() ? usersettings.locale() : i18n.remembered());
            });
        };

        self.submitLocale = function () {
            var code = self.locale();

            self.busy(true);

            return (session.isSignedIn() ? usersettings.saveLocale(code) : Promise.resolve({ ok: true }))
                .then(function (result) {
                    if (!result.ok) {
                        self.message(result.message);

                        return;
                    }

                    return i18n.choose(code).then(function () {
                        self.message(i18n.t("profile.localeSaved"));
                    });
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.activate = function () {
            if (!session.isSignedIn()) {
                return loadLocales();
            }

            self.busy(true);

            return load()
                .then(function (result) {
                    self.message(result.message);

                    return loadLocales();
                })
                .finally(function () {
                    self.busy(false);
                });
        };
    };
});
