define(["ko", "models/auth", "models/session", "models/pager", "models/i18n"], function (ko, auth, session, createPager, i18n) {
    var presentActive = function (item) {
        return {
            id: item.id,
            isCurrent: item.isCurrent,
            remoteAddress: item.remoteAddress,
            userAgent: item.userAgent,
            startedAt: i18n.dateTime(item.startedAt),
            lastActiveAt: i18n.dateTime(item.lastActiveAt),
            expiresAt: i18n.dateTime(item.expiresAt)
        };
    };

    var presentEnded = function (item) {
        return {
            id: item.id,
            remoteAddress: item.remoteAddress,
            userAgent: item.userAgent,
            startedAt: i18n.dateTime(item.startedAt),
            endedAt: i18n.dateTime(item.endedAt),
            reason: item.signedOut ? i18n.t("sessions.endReason.signedOut") : i18n.t("sessions.endReason.expired")
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
            var key = self.showEnded() ? "sessions.hideOld" : "sessions.showOld";

            return i18n.t(key, [self.endedPager.total()]);
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
            if (!window.confirm(i18n.t("sessions.confirmSignOutAll"))) {
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
