define(["ko"], function (ko) {
    var locales = ko.observableArray([]);
    var current = ko.observable(null);
    var loadFailed = ko.observable(false);
    var pendingMedia = new Set();
    var mediaRevision = ko.observable(0);
    var assignmentRevision = ko.observable(0);

    var media = function (value, locale) {
        if (!value) {
            return value;
        }
        var url;
        try {
            url = new URL(value, window.location.href);
        } catch (error) {
            return value;
        }
        var paths = /^\/(?:sites|news|articles|galleries|carousels|layouts)\/images\/|^\/downloads\/files\/|^\/sport\/(?:teams|players)\/images\//;
        if (url.origin !== window.location.origin || !paths.test(url.pathname) || url.searchParams.has("contentLocale") || pendingMedia.has(url.pathname) || !locale) {
            return value;
        }
        url.searchParams.set("contentLocale", locale);
        return value.charAt(0) === "/" ? url.pathname + url.search + url.hash : url.href;
    };

    var trackMedia = function (data) {
        if (Array.isArray(data)) {
            data.forEach(trackMedia);
        } else if (data && typeof data === "object") {
            Object.keys(data).forEach(function (key) {
                if (typeof data[key] === "string" && /url$/i.test(key)) {
                    try {
                        var url = new URL(data[key], window.location.href);
                        if (url.origin === window.location.origin) {
                            pendingMedia.add(url.pathname);
                        }
                    } catch (error) {
                        return;
                    }
                } else {
                    trackMedia(data[key]);
                }
            });
        }
    };

    var clearMedia = function () {
        if (pendingMedia.size) {
            pendingMedia.clear();
            mediaRevision(mediaRevision() + 1);
        }
    };

    var match = function (code) {
        if (!code) {
            return null;
        }
        var exact = locales().find(function (item) {
            return item.code.toLowerCase() === code.toLowerCase();
        });
        if (exact) {
            return exact.code;
        }
        var language = code.split("-")[0].toLowerCase();
        var sameLanguage = locales().find(function (item) {
            return item.code.split("-")[0].toLowerCase() === language;
        });
        return sameLanguage ? sameLanguage.code : null;
    };

    return {
        locales: locales,
        current: current,
        loadFailed: loadFailed,
        media: media,
        mediaRevision: mediaRevision,
        assignmentRevision: assignmentRevision,
        trackMedia: trackMedia,
        clearMedia: clearMedia,
        defaultLocale: function (preferences, fallback) {
            var preferred = (preferences || []).map(match).find(Boolean);
            if (preferred) {
                return preferred;
            }
            return match(fallback) || locales()[0] && locales()[0].code || null;
        }
    };
});
