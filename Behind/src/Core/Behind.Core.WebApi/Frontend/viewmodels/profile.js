define(["ko", "models/auth", "models/session", "models/user", "models/about", "models/usersettings", "models/i18n", "models/profilepicture", "models/profiles"], function (ko, auth, session, user, about, usersettings, i18n, profilepicture, profiles) {
    return function ProfileViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.displayName = ko.observable("");
        self.userId = ko.observable("");
        self.birthday = ko.observable("");
        self.hideEmail = ko.observable(true);
        self.detailsLoaded = ko.observable(false);
        self.publicHref = ko.pureComputed(function () {
            return user.href(self.userId());
        });
        self.emails = ko.observableArray([]);
        self.locales = ko.observableArray([]);
        self.locale = ko.observable("");
        var savedLocale = "";
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
            return user.label({ id: self.userId(), displayName: self.displayName() });
        });

        var load = function () {
            return auth.loadSession().then(function (result) {
                if (result.ok) {
                    self.displayName(result.data.user.displayName);
                    self.userId(result.data.user.id);
                    self.emails(result.data.user.emails);
                }

                return result;
            });
        };

        self.submit = function () {
            if (!self.detailsLoaded()) {
                self.message(i18n.t("common.error.generic"));
                return Promise.resolve();
            }

            self.busy(true);

            return Promise.all([
                auth.changeDisplayName(self.displayName()),
                profiles.save(self.birthday(), self.hideEmail())
            ]).then(function (results) {
                var failure = results.find(function (result) {
                    return !result.ok;
                });

                if (failure) {
                    self.message(failure.message);
                    return;
                }

                return load().then(function () {
                    self.message(i18n.t("profile.saved"));
                });
            }).catch(function (error) {
                self.message(error.message);
            }).finally(function () {
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
                savedLocale = self.locale();
            });
        };

        self.changeLocale = function () {
            var code = self.locale();
            var saved = false;

            self.busy(true);

            return (session.isSignedIn() ? usersettings.saveLocale(code) : Promise.resolve({ ok: true }))
                .then(function (result) {
                    if (!result.ok) {
                        self.locale(savedLocale);
                        self.message(result.message);

                        return;
                    }

                    saved = true;

                    return i18n.choose(code).then(function () {
                        savedLocale = code;
                        self.message(i18n.t("profile.localeSaved"));
                    });
                })
                .catch(function (error) {
                    if (!saved) {
                        self.locale(savedLocale);
                    }

                    self.message(error.message);
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
                        profiles.details().then(function (details) {
                            if (!details.ok) {
                                self.message(details.message);
                                return;
                            }

                            self.birthday(details.data.birthday || "");
                            self.hideEmail(details.data.hideEmail);
                            self.detailsLoaded(true);
                        }),
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
