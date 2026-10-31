define(["ko", "models/auth", "models/session"], function (ko, auth, session) {
    var formatDate = function (value) {
        return value ? new Date(value).toLocaleString() : "";
    };

    var formatSpan = function (value) {
        var match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(value || "");

        if (!match) {
            return "";
        }

        var days = Number(match[1] || 0);
        var parts = [
            [days, "d"],
            [Number(match[2]), "h"],
            [Number(match[3]), "m"],
            [Number(match[4]), "s"]
        ];

        return parts
            .filter(function (part) {
                return part[0] > 0;
            })
            .map(function (part) {
                return part[0] + part[1];
            })
            .join(" ") || "0s";
    };

    var present = function (data) {
        return {
            sessionId: data.sessionId,
            displayName: data.user.displayName,
            userId: data.user.id,
            emails: data.user.emails,
            roles: data.user.roles.map(function (role) {
                return { name: role.name, grants: role.grants.join(", ") };
            }),
            startedAt: formatDate(data.session.startedAt),
            length: formatSpan(data.session.length),
            accessTokenIssuedAt: formatDate(data.session.accessTokenIssuedAt),
            accessTokenExpiresAt: formatDate(data.session.accessTokenExpiresAt),
            accessTokenRemaining: formatSpan(data.session.accessTokenRemaining),
            expiresAt: formatDate(data.session.expiresAt),
            remaining: formatSpan(data.session.remaining),
            remoteAddress: data.session.remoteAddress,
            userAgent: data.session.userAgent
        };
    };

    return function SessionViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.details = ko.observable(null);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.refresh = function () {
            if (!session.isSignedIn()) {
                self.details(null);
                return Promise.resolve();
            }

            self.busy(true);

            return auth.loadSession()
                .then(function (result) {
                    self.message(result.message);
                    self.details(result.ok ? present(result.data) : null);
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
