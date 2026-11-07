define(["jquery", "jquery-migrate", "ko", "sammy", "viewmodels/header", "viewmodels/footer", "models/accounts", "models/branding", "models/session", "models/usersettings", "models/restriction", "models/i18n"], function ($, migrate, ko, Sammy, HeaderViewModel, FooterViewModel, accounts, branding, session, usersettings, restriction, i18n) {
    var routes = {
        "#/": "controllers/home",
        "#/sign-in": "controllers/signin",
        "#/sign-up": "controllers/signup",
        "#/session": "controllers/session",
        "#/sessions": "controllers/sessions",
        "#/profile": "controllers/profile",
        "#/profile/password": "controllers/profilepassword",
        "#/profile/two-factor": "controllers/twofactor",
        "#/users": "controllers/users",
        "#/roles": "controllers/roles",
        "#/audit-log": "controllers/auditlog",
        "#/settings": "controllers/settings",
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
        "#/manage/route-aliases": "controllers/routealiases",
        "#/manage/access-settings": "controllers/accesssettings",
        "#/manage/raw-database": "controllers/rawdatabase",
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
        "#/site/:slug": "controllers/site"
    };

    var openControllers = [
        "controllers/signin",
        "controllers/signup",
        "controllers/magiclink",
        "controllers/confirmemail",
        "controllers/profilepassword"
    ];

    var restrictionController = "controllers/restriction";
    var aliasRoute = "#/:slug";
    var siteController = "controllers/site";
    var aliasController = "controllers/routealias";
    var following = false;

    ko.components.register("pager", { require: "components/pager" });
    ko.components.register("captcha", { require: "components/captcha" });
    ko.components.register("two-factor-challenge", { require: "components/twofactorchallenge" });
    ko.components.register("recovery-codes", { require: "components/recoverycodes" });
    ko.components.register("totp-enrollment", { require: "components/totpenrollment" });
    ko.components.register("letter-pager", { require: "components/letterpager" });
    ko.components.register("access-editor", { require: "components/accesseditor" });
    ko.components.register("download-tree", { require: "components/downloadtree" });
    ko.components.register("menu-items", { require: "components/menuitems" });
    ko.components.register("menu-nav", { require: "components/menunav" });
    ko.components.register("carousel-editor", { require: "components/carouseleditor" });
    ko.components.register("site-sections-editor", { require: "components/sectionseditor" });

    var container = function () {
        return $("#main-container");
    };

    var context = {
        renderView: function (view, viewModel) {
            var element = container();
            ko.cleanNode(element[0]);
            element.html(view);
            window.scrollTo(0, 0);
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
        },
        follow: function (hash) {
            if (!router.lookupRoute("get", hash)) {
                return false;
            }

            following = true;

            try {
                router.runRoute("get", hash);
            } finally {
                following = false;
            }

            return true;
        },
        fallback: function (params) {
            execute(siteController, params);
        }
    };

    var execute = function (requested, params) {
        var controllerName = restriction.restricted() && openControllers.indexOf(requested) < 0
            ? restrictionController
            : requested;

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

        app.get(aliasRoute, function () {
            execute(following ? siteController : aliasController, this.params.toHash());
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

    var syncRestriction = function () {
        return restriction.load();
    };

    var syncUserSettings = function () {
        return session.isSignedIn() ? usersettings.load() : usersettings.clear();
    };

    return {
        start: function () {
            applyColorScheme();
            accounts.load();
            branding.title.subscribe(function (title) {
                document.title = title;
            });

            return branding.load()
                .then(function () {
                    i18n.useDefault(branding.locale());

                    return Promise.all([syncUserSettings(), syncRestriction()]);
                })
                .then(function () {
                    i18n.useAccount(usersettings.locale());
                    usersettings.locale.subscribe(i18n.useAccount);
                    session.isSignedIn.subscribe(syncUserSettings);
                    session.isImpersonating.subscribe(syncUserSettings);
                    session.isSignedIn.subscribe(syncRestriction);
                    session.isImpersonating.subscribe(syncRestriction);
                    restriction.restricted.subscribe(function () {
                        router.runRoute("get", window.location.hash || "#/");
                    });

                    return i18n.apply();
                })
                .then(function () {
                    ko.applyBindings(new HeaderViewModel(context), $("#header-container")[0]);

                    var footer = new FooterViewModel();

                    ko.applyBindings(footer, $("#footer-container")[0]);
                    footer.activate();
                    router.run("#/");
                });
        }
    };
});
