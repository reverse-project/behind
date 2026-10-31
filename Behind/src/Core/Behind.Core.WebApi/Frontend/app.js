define(["jquery", "jquery-migrate", "ko", "sammy"], function ($, migrate, ko, Sammy) {
    var routes = {
        "#/": "controllers/index",
        "#/sign-in": "controllers/signin",
        "#/sign-up": "controllers/signup",
        "#/sign/in/magiclink": "controllers/magiclink"
    };

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

    return {
        start: function () {
            router.run("#/");
        }
    };
});
