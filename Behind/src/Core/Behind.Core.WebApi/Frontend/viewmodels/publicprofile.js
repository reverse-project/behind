define(["ko", "models/profiles", "models/auth", "models/i18n"], function (ko, profiles, auth, i18n) {
    return function PublicProfileViewModel(context) {
        var self = this;
        var id = context.params.id;
        self.profileId = id;

        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.profile = ko.observable(null);
        self.banning = ko.observable(false);
        self.banMessage = ko.observable("");
        self.t = i18n.t;
        self.dateTime = i18n.dateTime;
        self.banReason = i18n.banReason;

        self.refresh = function () {
            self.busy(true);
            self.profile(null);

            return profiles.get(id).then(function (result) {
                self.message(result.message);

                if (result.ok) {
                    self.profile(result.data);
                }

                return result;
            }).catch(function (error) {
                self.message(error.message);
                return { ok: false };
            }).finally(function () {
                self.busy(false);
            });
        };

        var perform = function (action, refresh) {
            self.busy(true);

            return action().then(function (result) {
                return (result.ok && refresh !== false ? self.refresh() : Promise.resolve()).then(function (refreshed) {
                    if (!refreshed || refreshed.ok) {
                        self.message(result.message);
                    }

                    if (result.ok) {
                        self.banning(false);
                        self.banMessage("");
                    }
                });
            }).catch(function (error) {
                self.message(error.message);
            }).finally(function () {
                self.busy(false);
            });
        };

        self.startBan = function () {
            self.banning(true);
        };
        self.cancelBan = function () {
            self.banning(false);
            self.banMessage("");
        };
        self.ban = function () {
            return perform(function () {
                return auth.banUser(id, self.banMessage().trim());
            });
        };
        self.unban = function () {
            return perform(function () {
                return auth.unbanUser(id);
            });
        };
        self.impersonate = function () {
            if (!window.confirm(i18n.t("users.confirmImpersonate", [self.profile().displayName]))) {
                return Promise.resolve();
            }

            return perform(function () {
                return auth.impersonateUser(id).then(function (result) {
                    if (result.ok) {
                        context.navigate("#/");
                    }

                    return result;
                });
            }, false);
        };
        self.activate = self.refresh;
    };
});
