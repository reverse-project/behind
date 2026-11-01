define(["models/http"], function (http) {
    var unknownVersion = "";

    var load = function (url, pick, fallback) {
        return http.get(url).then(function (response) {
            return response.ok ? pick(response.data) : fallback;
        });
    };

    return {
        loadVersion: function () {
            return load("/about", function (data) {
                return data.version;
            }, unknownVersion);
        },

        loadChangelog: function () {
            return http.get("/changelog.json").then(function (response) {
                return response.ok && Array.isArray(response.data)
                    ? { ok: true, releases: response.data }
                    : { ok: false, releases: [] };
            });
        }
    };
});
