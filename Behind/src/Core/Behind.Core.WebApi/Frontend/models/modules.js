define(["ko", "models/http"], function (ko, http) {
    var modules = ko.observableArray([]);
    var loadedStyles = {};

    var collect = function (select) {
        return modules().reduce(function (all, module) {
            return all.concat(select(module));
        }, []);
    };

    var applyStyle = function (path) {
        if (loadedStyles[path]) {
            return;
        }

        loadedStyles[path] = true;

        var link = document.createElement("link");

        link.rel = "stylesheet";
        link.href = "/" + path;
        document.head.appendChild(link);
    };

    var listed = function (response) {
        return response.ok && response.data && Array.isArray(response.data.modules) ? response.data.modules : [];
    };

    return {
        modules: modules,

        routes: function () {
            return collect(function (module) {
                return module.routes;
            });
        },

        administrationPages: function () {
            return collect(function (module) {
                return module.administrationPages;
            });
        },

        widgets: function () {
            return collect(function (module) {
                return module.widgets || [];
            });
        },

        localeUrls: function (code) {
            return modules().map(function (module) {
                return "/" + module.path + "locales/" + encodeURIComponent(code) + ".json";
            });
        },

        load: function () {
            return http.get("/modules").then(function (response) {
                modules(listed(response));
                collect(function (module) {
                    return module.styles;
                }).forEach(applyStyle);
            });
        }
    };
});
