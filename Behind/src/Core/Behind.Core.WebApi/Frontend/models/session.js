define(["ko"], function (ko) {
    var storageKey = "behind.session";

    var restore = function () {
        try {
            return JSON.parse(window.sessionStorage.getItem(storageKey));
        } catch (error) {
            return null;
        }
    };

    var tokens = ko.observable(restore());

    return {
        isSignedIn: ko.pureComputed(function () {
            return tokens() !== null;
        }),
        accessToken: function () {
            var current = tokens();
            return current ? current.accessToken : null;
        },
        start: function (response) {
            var value = {
                accessToken: response.accessToken,
                refreshToken: response.refreshToken,
                expires: response.expires
            };
            window.sessionStorage.setItem(storageKey, JSON.stringify(value));
            tokens(value);
        },
        end: function () {
            window.sessionStorage.removeItem(storageKey);
            tokens(null);
        }
    };
});
