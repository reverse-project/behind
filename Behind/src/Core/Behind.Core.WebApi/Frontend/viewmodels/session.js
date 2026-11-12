define(["ko", "models/auth", "models/session", "models/user", "models/i18n"], function (ko, auth, session, user, i18n) {
    var elevationTargets = [
        { value: "administrator", label: "session.target.administrator", role: "Administrators" },
        { value: "developer", label: "session.target.developer", role: "Developers" }
    ];

    var spanUnits = ["session.span.days", "session.span.hours", "session.span.minutes", "session.span.seconds"];

    var formatSpan = function (value) {
        var match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(value || "");

        if (!match) {
            return "";
        }

        var days = Number(match[1] || 0);
        var amounts = [days, Number(match[2]), Number(match[3]), Number(match[4])];

        return amounts
            .map(function (amount, index) {
                return [amount, spanUnits[index]];
            })
            .filter(function (part) {
                return part[0] > 0;
            })
            .map(function (part) {
                return i18n.t(part[1], [part[0]]);
            })
            .join(" ") || i18n.t("session.span.zero");
    };

    var present = function (data) {
        return {
            sessionId: data.sessionId,
            displayName: user.label(data.user),
            profileHref: user.href(data.user.id),
            userId: data.user.id,
            emails: data.user.emails,
            roles: data.user.roles.map(function (role) {
                return { name: role.name, grants: role.grants.join(", ") };
            }),
            roleNames: data.user.roles.map(function (role) {
                return role.name;
            }),
            startedAt: i18n.dateTime(data.session.startedAt),
            length: formatSpan(data.session.length),
            accessTokenIssuedAt: i18n.dateTime(data.session.accessTokenIssuedAt),
            accessTokenExpiresAt: i18n.dateTime(data.session.accessTokenExpiresAt),
            accessTokenRemaining: formatSpan(data.session.accessTokenRemaining),
            expiresAt: i18n.dateTime(data.session.expiresAt),
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

        self.availableTargets = ko.pureComputed(function () {
            var details = self.details();

            return details === null
                ? []
                : elevationTargets
                    .filter(function (target) {
                        return details.roleNames.indexOf(target.role) < 0;
                    })
                    .map(function (target) {
                        return { value: target.value, label: i18n.t(target.label) };
                    });
        });

        self.canElevate = ko.pureComputed(function () {
            return self.availableTargets().length > 0;
        });

        self.target = ko.observable("administrator");

        self.code = ko.observable("");

        self.elevate = function () {
            self.busy(true);

            return auth.elevate(self.target(), self.code().trim())
                .then(function (result) {
                    var refreshed = result.ok ? self.refresh() : Promise.resolve();

                    return refreshed.then(function () {
                        self.code("");
                        self.message(result.message);
                    });
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
