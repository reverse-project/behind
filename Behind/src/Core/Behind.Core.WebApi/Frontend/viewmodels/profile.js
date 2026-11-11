define(["ko", "models/auth", "models/session", "models/user", "models/about", "models/usersettings", "models/i18n", "models/profilepicture"], function (ko, auth, session, user, about, usersettings, i18n, profilepicture) {
    return function ProfileViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.displayName = ko.observable("");
        self.emails = ko.observableArray([]);
        self.locales = ko.observableArray([]);
        self.locale = ko.observable("");
        self.settingNewPassword = ko.observable(false);
        self.passwordFocus = ko.observable(false);
        self.pictureUrl = ko.observable(null);

        var pictureAction = function (action) {
            self.busy(true);

            return action().then(function () {
                self.message(i18n.t("profile.pictureSaved"));
            }).catch(function (error) {
                self.message(error.message);
            }).finally(function () {
                self.busy(false);
            });
        };
        self.choosePicture = function (data, event) {
            var file = event.target.files[0];

            if (!file) {
                return;
            }

            return pictureAction(function () {
                return profilepicture.upload(file).then(function (image) {
                    self.pictureUrl(image.url);
                    event.target.value = "";
                });
            });
        };
        self.removePicture = function () {
            return pictureAction(function () {
                return profilepicture.remove().then(function () {
                    self.pictureUrl(null);
                });
            });
        };

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
                    self.passwordFocus(self.settingNewPassword());

                    return Promise.all([
                        loadLocales(),
                        profilepicture.get().then(function (picture) {
                            self.pictureUrl(picture.url);
                        })
                    ]);
                })
                .catch(function (error) {
                    self.message(error.message);
                })
                .finally(function () {
                    self.busy(false);
                });
        };
    };
});
