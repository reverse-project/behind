define(["models/http"], function (http) {
    var unknownVersion = "";

    var load = function (url, pick, fallback) {
        return http.get(url).then(function (response) {
            return response.ok ? pick(response.data) : fallback;
        });
    };

    var loadList = function (url) {
        return http.get(url).then(function (response) {
            return response.ok && Array.isArray(response.data)
                ? { ok: true, items: response.data }
                : { ok: false, items: [] };
        });
    };

    return {
        loadVersion: function () {
            return load("/about", function (data) {
                return data.version;
            }, unknownVersion);
        },

        loadChangelog: function () {
            return loadList("/changelog.json").then(function (result) {
                return { ok: result.ok, releases: result.items };
            });
        },

        loadLicenses: function () {
            return loadList("/licenses.json").then(function (result) {
                return { ok: result.ok, licenses: result.items };
            });
        }
    };
});
