define(["jquery", "jquery-migrate", "ko", "sammy", "viewmodels/header"], function ($, migrate, ko, Sammy, HeaderViewModel) {
    var routes = {
        "#/": "controllers/index",
        "#/sign-in": "controllers/signin",
        "#/sign-up": "controllers/signup",
        "#/session": "controllers/session",
        "#/sessions": "controllers/sessions",
        "#/profile": "controllers/profile",
        "#/users": "controllers/users",
        "#/roles": "controllers/roles",
        "#/audit-log": "controllers/auditlog",
        "#/administration": "controllers/administration",
        "#/wall-of-shame": "controllers/wallofshame",
        "#/sign/in/magiclink": "controllers/magiclink"
    };

    ko.components.register("pager", { require: "components/pager" });
    ko.components.register("letter-pager", { require: "components/letterpager" });
    ko.components.register("access-editor", { require: "components/accesseditor" });

    var container = function () {
        return $("#main-container");
    };

    var context = {
        renderView: function (view, viewModel) {
            var element = container();
            ko.cleanNode(element[0]);
            element.html(view);
            ko.applyBindings(viewModel, element[0]);
        },
        navigate: function (hash) {
            window.location.hash = hash;
        },
        replace: function (hash) {
            window.location.replace(hash);
        },
        scrub: function (hash) {
            window.history.replaceState(null, "", window.location.pathname + window.location.search + hash);
        }
    };

    var execute = function (controllerName, params) {
        require([controllerName], function (Controller) {
            new Controller($.extend({}, context, { params: params })).exec();
        });
    };

    var router = Sammy(function () {
        var app = this;

        Object.keys(routes).forEach(function (path) {
            app.get(path, function () {
                execute(routes[path], this.params.toHash());
            });
        });
    });

    var applyColorScheme = function () {
        var query = window.matchMedia("(prefers-color-scheme: dark)");
        var apply = function () {
            document.documentElement.setAttribute("data-bs-theme", query.matches ? "dark" : "light");
        };

        apply();
        query.addEventListener("change", apply);
    };

    return {
        start: function () {
            applyColorScheme();
            ko.applyBindings(new HeaderViewModel(context), $("#header-container")[0]);
            router.run("#/");
        }
    };
});
