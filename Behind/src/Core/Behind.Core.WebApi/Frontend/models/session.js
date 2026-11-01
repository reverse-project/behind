define(["jquery", "ko"], function ($, ko) {
    var storageKey = "behind.session";
    var impersonatorKey = "behind.impersonator";

    var restore = function (key) {
        try {
            return JSON.parse(window.sessionStorage.getItem(key));
        } catch (error) {
            return null;
        }
    };

    var tokens = ko.observable(restore(storageKey));
    var stashed = ko.observable(restore(impersonatorKey));

    var persist = function (value) {
        window.sessionStorage.setItem(storageKey, JSON.stringify(value));
        tokens(value);
    };

    var stash = function (value) {
        if (value === null) {
            window.sessionStorage.removeItem(impersonatorKey);
        } else {
            window.sessionStorage.setItem(impersonatorKey, JSON.stringify(value));
        }

        stashed(value);
    };

    var begin = function (response) {
        return {
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            expires: response.expires
        };
    };

    return {
        isSignedIn: ko.pureComputed(function () {
            return tokens() !== null;
        }),
        accessToken: function () {
            var current = tokens();
            return current ? current.accessToken : null;
        },
        refreshHeaders: function () {
            var current = tokens();
            return current && current.refreshToken ? { "X-Refresh-Token": current.refreshToken } : {};
        },
        displayName: ko.pureComputed(function () {
            var current = tokens();
            return current ? current.displayName || "" : "";
        }),
        identify: function (displayName, grants) {
            var current = tokens();

            if (current) {
                persist($.extend({}, current, { displayName: displayName, grants: grants }));
            }
        },
        grants: ko.pureComputed(function () {
            var current = tokens();
            return current && current.grants ? current.grants : null;
        }),
        hasGrant: function (name) {
            var current = tokens();
            return !!current && !!current.grants && current.grants.indexOf(name) >= 0;
        },
        renew: function (refreshed) {
            var current = tokens();

            if (current) {
                persist($.extend({}, current, { accessToken: refreshed.accessToken, expires: refreshed.expires }));
            }
        },
        start: function (response) {
            persist(begin(response));
        },
        isImpersonating: ko.pureComputed(function () {
            return stashed() !== null && tokens() !== null;
        }),
        impersonate: function (response) {
            stash(tokens());
            persist(begin(response));
        },
        stopImpersonating: function () {
            var original = stashed();

            stash(null);

            if (original) {
                persist(original);
            } else {
                window.sessionStorage.removeItem(storageKey);
                tokens(null);
            }
        },
        end: function () {
            window.sessionStorage.removeItem(storageKey);
            tokens(null);
            stash(null);
        }
    };
});
