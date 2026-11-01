define(["ko", "models/auth", "models/session", "models/user"], function (ko, auth, session, user) {
    return function ProfileViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.displayName = ko.observable("");
        self.emails = ko.observableArray([]);

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

        self.activate = function () {
            if (!session.isSignedIn()) {
                return Promise.resolve();
            }

            self.busy(true);

            return load()
                .then(function (result) {
                    self.message(result.message);
                })
                .finally(function () {
                    self.busy(false);
                });
        };
    };
});
