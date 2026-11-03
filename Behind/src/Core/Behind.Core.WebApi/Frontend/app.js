define(["jquery", "jquery-migrate", "ko", "sammy", "viewmodels/header", "viewmodels/footer"], function ($, migrate, ko, Sammy, HeaderViewModel, FooterViewModel) {
    var routes = {
        "#/": "controllers/home",
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
        "#/sites": "controllers/sites",
        "#/sites/new": "controllers/siteeditor",
        "#/sites/:id": "controllers/siteeditor",
        "#/news": "controllers/news",
        "#/news/:id": "controllers/newsarticle",
        "#/manage/news": "controllers/newsmanager",
        "#/manage/news/new": "controllers/newseditor",
        "#/manage/news/:id": "controllers/newseditor",
        "#/manage/news-categories": "controllers/newscategories",
        "#/articles": "controllers/articles",
        "#/articles/:id": "controllers/articlereader",
        "#/manage/articles": "controllers/articlemanager",
        "#/manage/articles/new": "controllers/articleeditor",
        "#/manage/articles/:id": "controllers/articleeditor",
        "#/manage/article-categories": "controllers/articlecategories",
        "#/galleries": "controllers/galleries",
        "#/galleries/:id": "controllers/galleryviewer",
        "#/manage/galleries": "controllers/gallerymanager",
        "#/manage/galleries/new": "controllers/galleryeditor",
        "#/manage/galleries/:id": "controllers/galleryeditor",
        "#/downloads": "controllers/downloads",
        "#/manage/downloads": "controllers/downloadmanager",
        "#/manage/menus": "controllers/menus",
        "#/manage/menus/:id": "controllers/menuitems",
        "#/changelog": "controllers/changelog",
        "#/licenses": "controllers/licenses",
        "#/sign/in/magiclink": "controllers/magiclink",
        "#/sign/up/confirm": "controllers/confirmemail"
    };

    var patternRoutes = [
        { pattern: /(?:^|\/)#\/downloads\/(.*)$/, controller: "controllers/downloads" },
        { pattern: /(?:^|\/)#\/manage\/downloads\/(.*)$/, controller: "controllers/downloadmanager" }
    ];

    var siteRoutes = {
        "#/site/:slug": "controllers/site",
        "#/:slug": "controllers/site"
    };

    ko.components.register("pager", { require: "components/pager" });
    ko.components.register("letter-pager", { require: "components/letterpager" });
    ko.components.register("access-editor", { require: "components/accesseditor" });
    ko.components.register("download-tree", { require: "components/downloadtree" });
    ko.components.register("menu-items", { require: "components/menuitems" });
    ko.components.register("menu-nav", { require: "components/menunav" });

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
            router.last_location = ["get", router.getLocation()];
        }
    };

    var execute = function (controllerName, params) {
        require([controllerName], function (Controller) {
            new Controller($.extend({}, context, { params: params })).exec();
        });
    };

    var ignoreFormSubmissions = function () {
        return false;
    };

    var router = Sammy(function () {
        var app = this;

        app._checkFormSubmission = ignoreFormSubmissions;

        patternRoutes.forEach(function (route) {
            app.get(route.pattern, function () {
                execute(route.controller, this.params.toHash());
            });
        });

        [routes, siteRoutes].forEach(function (table) {
            Object.keys(table).forEach(function (path) {
                app.get(path, function () {
                    execute(table[path], this.params.toHash());
                });
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

            var footer = new FooterViewModel();

            ko.applyBindings(footer, $("#footer-container")[0]);
            footer.activate();
            router.run("#/");
        }
    };
});
