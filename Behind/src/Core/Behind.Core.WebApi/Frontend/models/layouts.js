define(["models/http", "models/invalidation", "models/results", "models/contentlocale"], function (http, invalidation, results, contentLocale) {
    var configuration = null;
    var configurationLocale = null;
    var pending = {};
    var imageFailures = {
        400: "content.error.imageRequired",
        401: results.sessionExpired,
        403: "content.error.imageForbidden",
        413: "content.error.imageTooLarge",
        415: "content.error.imageType"
    };
    var outcome = results.create({});

    var remember = function (data, locale) {
        if (locale === contentLocale.current() && (!configuration || configurationLocale !== locale || data.revision >= configuration.revision)) {
            configuration = {
                schemaVersion: data.schemaVersion,
                revision: data.revision,
                defaultLayout: data.defaultLayout,
                layouts: data.layouts.filter(function (layout) {
                    return !layout.deleted;
                }),
                assignments: data.assignments
            };
            configurationLocale = locale;
        }
        return configuration;
    };

    var load = function () {
        var locale = contentLocale.current();
        var key = locale || "";
        if (!pending[key]) {
            var request = http.get("/layouts").then(function (response) {
                if (!response.ok) {
                    throw new Error("layouts.error.load");
                }

                return remember(response.data, locale);
            }).finally(function () {
                if (pending[key] === request) {
                    delete pending[key];
                }
            });
            pending[key] = request;
        }

        return pending[key];
    };

    var match = function (data, route, accept) {
        var locale = contentLocale.current();
        var matches = data.assignments.filter(function (assignment) {
            return (assignment.route.endsWith("*")
                ? route.indexOf(assignment.route.slice(0, -1)) === 0
                : assignment.route === route) && (!accept || accept(assignment.route));
        }).sort(function (left, right) {
            var specificity = function (assignment) {
                return assignment.route.length * 2 + (assignment.route.endsWith("*") ? 0 : 1);
            };
            var leftLayout = data.layouts.find(function (layout) { return layout.id === left.layout; });
            var rightLayout = data.layouts.find(function (layout) { return layout.id === right.layout; });
            var localeMatch = function (layout) {
                return layout && (layout.locale || null) === locale ? 1 : 0;
            };
            return specificity(right) - specificity(left)
                || localeMatch(rightLayout) - localeMatch(leftLayout);
        });
        return matches.length ? matches[0] : null;
    };

    var find = function (data, id) {
        return data.layouts.find(function (layout) {
            return layout.id === id && !layout.deleted;
        });
    };

    var select = function (data, route) {
        var assignment = match(data, route);
        return find(data, assignment ? assignment.layout : data.defaultLayout);
    };

    var selectAssigned = function (data, route, accept) {
        var assignment = match(data, route, accept);
        return assignment ? find(data, assignment.layout) : undefined;
    };

    return {
        uploadImage: function (file) {
            var form = new FormData();
            form.append("file", file);
            return http.postForm("/layouts/images", form).then(function (response) {
                return outcome.loaded(response, imageFailures);
            });
        },
        clear: function () {
            configuration = null;
            configurationLocale = null;
            pending = {};
        },
        load: load,
        select: select,
        selectAssigned: selectAssigned,
        current: function () {
            return configurationLocale === contentLocale.current() ? configuration : null;
        },
        manage: function () {
            return http.get("/layouts/manage");
        },
        save: function (data, selectedId) {
            return http.post("/layouts", data).then(function (response) {
                if (!response.ok) {
                    return response;
                }

                pending = {};
                remember(response.data, contentLocale.current());
                var selected = response.data.layouts.find(function (layout) {
                    return layout.id === selectedId && !layout.deleted;
                });
                return invalidation.invalidate("layouts", { preview: selected || null }).then(function () {
                    return response;
                });
            });
        }
    };
});
