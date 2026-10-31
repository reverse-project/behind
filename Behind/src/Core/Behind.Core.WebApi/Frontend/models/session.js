define(["jquery", "ko"], function ($, ko) {
    var storageKey = "behind.session";

    var restore = function () {
        try {
            return JSON.parse(window.sessionStorage.getItem(storageKey));
        } catch (error) {
            return null;
        }
    };

    var tokens = ko.observable(restore());

    var persist = function (value) {
        window.sessionStorage.setItem(storageKey, JSON.stringify(value));
        tokens(value);
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
        identify: function (displayName) {
            var current = tokens();

            if (current) {
                persist($.extend({}, current, { displayName: displayName }));
            }
        },
        renew: function (refreshed) {
            var current = tokens();

            if (current) {
                persist($.extend({}, current, { accessToken: refreshed.accessToken, expires: refreshed.expires }));
            }
        },
        start: function (response) {
            var value = {
                accessToken: response.accessToken,
                refreshToken: response.refreshToken,
                expires: response.expires
            };
            persist(value);
        },
        end: function () {
            window.sessionStorage.removeItem(storageKey);
            tokens(null);
        }
    };
});
