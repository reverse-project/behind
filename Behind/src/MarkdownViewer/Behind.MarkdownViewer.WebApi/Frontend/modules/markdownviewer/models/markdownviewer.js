define(["models/http", "models/results", "models/i18n"], function (http, results, i18n) {
    var outcome = results.create({});
    var loaded = outcome.loaded;

    var listFailures = {};

    var pageFailures = {
        404: "markdownviewer.error.notFound"
    };

    var generateFailures = {
        401: results.sessionExpired,
        403: "markdownviewer.error.generateDenied"
    };

    var localized = function (url, locale) {
        return locale ? url + "?locale=" + encodeURIComponent(locale) : url;
    };

    var pageUrl = function (path, locale) {
        return localized("/markdownviewer/documents/" + path.split("/").map(encodeURIComponent).join("/"), locale);
    };

    var problems = function (response) {
        var errors = response.data && response.data.errors ? response.data.errors : [];

        return errors.map(function (code) {
            return i18n.tOr("markdownviewer.error." + code, code);
        }).join(" ");
    };

    return {
        documentRoute: function (path, locale) {
            return "#/docs/" + encodeURIComponent(path) + (locale ? "?locale=" + encodeURIComponent(locale) : "");
        },

        list: function (locale) {
            return http.get(localized("/markdownviewer/documents", locale)).then(function (response) {
                return loaded(response, listFailures);
            });
        },

        page: function (path, locale) {
            return http.get(pageUrl(path, locale)).then(function (response) {
                return loaded(response, pageFailures);
            });
        },

        generate: function () {
            return http.post("/markdownviewer/generate").then(function (response) {
                if (response.ok) {
                    var site = response.data;

                    return {
                        ok: true,
                        message: i18n.t("markdownviewer.done.generated", [site.pages, site.assets, site.removed, site.outputDirectory]),
                        data: site
                    };
                }

                var detail = problems(response);

                return detail !== "" ? { ok: false, message: detail } : outcome.failure(response, generateFailures);
            });
        }
    };
});
