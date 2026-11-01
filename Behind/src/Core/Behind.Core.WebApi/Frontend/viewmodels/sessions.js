define(["ko", "models/auth", "models/session", "models/pager"], function (ko, auth, session, createPager) {
    var formatDate = function (value) {
        return value ? new Date(value).toLocaleString() : "";
    };

    var presentActive = function (item) {
        return {
            id: item.id,
            isCurrent: item.isCurrent,
            remoteAddress: item.remoteAddress,
            userAgent: item.userAgent,
            startedAt: formatDate(item.startedAt),
            lastActiveAt: formatDate(item.lastActiveAt),
            expiresAt: formatDate(item.expiresAt)
        };
    };

    var presentEnded = function (item) {
        return {
            id: item.id,
            remoteAddress: item.remoteAddress,
            userAgent: item.userAgent,
            startedAt: formatDate(item.startedAt),
            endedAt: formatDate(item.endedAt),
            reason: item.signedOut ? "Signed out" : "Expired"
        };
    };

    var pageSize = 10;

    return function SessionsViewModel(context) {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.activePager = createPager(function () {
            return self.refresh();
        });
        self.endedPager = createPager(function () {
            return self.refresh();
        });
        self.active = ko.observableArray([]);
        self.ended = ko.observableArray([]);
        self.showEnded = ko.observable(false);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.hasEnded = ko.pureComputed(function () {
            return self.endedPager.total() > 0;
        });

        self.endedToggleLabel = ko.pureComputed(function () {
            return (self.showEnded() ? "Hide" : "Show") + " old sessions (" + self.endedPager.total() + ")";
        });

        self.toggleEnded = function () {
            self.showEnded(!self.showEnded());
        };

        self.refresh = function () {
            if (!session.isSignedIn()) {
                self.active([]);
                self.ended([]);
                return Promise.resolve();
            }

            self.busy(true);

            return auth.loadSessions({
                activePage: self.activePager.page(),
                endedPage: self.endedPager.page(),
                pageSize: pageSize
            })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.active([]);
                        self.ended([]);
                        return undefined;
                    }

                    if (self.activePager.isOutOfRange(result.data.active) || self.endedPager.isOutOfRange(result.data.ended)) {
                        self.activePager.clampIfOutOfRange(result.data.active);
                        self.endedPager.clampIfOutOfRange(result.data.ended);
                        return self.refresh();
                    }

                    self.activePager.update(result.data.active);
                    self.endedPager.update(result.data.ended);
                    self.active(result.data.active.items.map(presentActive));
                    self.ended(result.data.ended.items.map(presentEnded));
                    return undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.signOutAll = function () {
            if (!window.confirm("Sign out of all sessions, including this one?")) {
                return Promise.resolve();
            }

            self.busy(true);

            return auth.signOutAll()
                .then(function (result) {
                    if (result.ok) {
                        context.navigate("#/sign-in");
                        return;
                    }

                    self.message(result.message);
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.activate = function () {
            return self.refresh();
        };
    };
});
