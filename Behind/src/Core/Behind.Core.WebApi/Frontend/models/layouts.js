define(["models/http", "models/invalidation"], function (http, invalidation) {
    var configuration = null;
    var pending = null;

    var remember = function (data) {
        if (!configuration || data.revision >= configuration.revision) {
            configuration = {
                revision: data.revision,
                defaultLayout: data.defaultLayout,
                layouts: data.layouts.filter(function (layout) {
                    return !layout.deleted;
                }),
                assignments: data.assignments
            };
        }
        return configuration;
    };

    var load = function () {
        if (!pending) {
            pending = http.get("/layouts").then(function (response) {
                if (!response.ok) {
                    throw new Error("layouts.error.load");
                }

                return remember(response.data);
            }).finally(function () {
                pending = null;
            });
        }

        return pending;
    };

    var select = function (data, route) {
        var matches = data.assignments.filter(function (assignment) {
            return assignment.route.endsWith("*")
                ? route.indexOf(assignment.route.slice(0, -1)) === 0
                : assignment.route === route;
        }).sort(function (left, right) {
            var specificity = function (assignment) {
                return assignment.route.length * 2 + (assignment.route.endsWith("*") ? 0 : 1);
            };
            return specificity(right) - specificity(left);
        });
        var id = matches.length ? matches[0].layout : data.defaultLayout;
        return data.layouts.find(function (layout) {
            return layout.id === id && !layout.deleted;
        });
    };

    return {
        load: load,
        select: select,
        current: function () {
            return configuration;
        },
        manage: function () {
            return http.get("/layouts/manage");
        },
        save: function (data) {
            return http.post("/layouts", data).then(function (response) {
                if (!response.ok) {
                    return response;
                }

                remember(response.data);
                return invalidation.invalidate("layouts").then(function () {
                    return response;
                });
            });
        }
    };
});
