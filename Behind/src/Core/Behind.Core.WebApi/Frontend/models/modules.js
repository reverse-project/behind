define(["ko", "models/http"], function (ko, http) {
    var modules = ko.observableArray([]);
    var availableGrants = ko.observableArray([]);
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

        visibleGrants: function (names) {
            var available = availableGrants();
            return names.filter(function (name) {
                return available.indexOf(name) >= 0;
            });
        },

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
                if (!response.ok || !response.data || !Array.isArray(response.data.availableGrants)) {
                    console.error("Module access visibility could not be loaded");
                }
                availableGrants(response.ok && response.data && Array.isArray(response.data.availableGrants)
                    ? response.data.availableGrants : []);
                collect(function (module) {
                    return module.styles;
                }).forEach(applyStyle);
            });
        }
    };
});
