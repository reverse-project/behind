define(["jquery", "jquery-migrate", "ko", "sammy", "models/accounts", "models/branding", "models/session", "models/usersettings", "models/restriction", "models/i18n", "models/modules", "models/layoutshell", "models/contentlocale", "models/about", "bindings/contentlocalescope"], function ($, migrate, ko, Sammy, accounts, branding, session, usersettings, restriction, i18n, modules, layoutShell, contentLocale, about) {
    var routes = {
        "#/": "controllers/home",
        "#/sign-in": "controllers/signin",
        "#/sign-up": "controllers/signup",
        "#/session": "controllers/session",
        "#/sessions": "controllers/sessions",
        "#/profile": "controllers/profile",
        "#/profile/password": "controllers/profilepassword",
        "#/profile/two-factor": "controllers/twofactor",
        "#/user/:id": "controllers/publicprofile",
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
        "#/manage/layouts": "controllers/layouts",
        "#/manage/menus/:id": "controllers/menuitems",
        "#/manage/route-aliases": "controllers/routealiases",
        "#/manage/messages": "controllers/messages",
        "#/manage/access-settings": "controllers/accesssettings",
        "#/manage/raw-database": "controllers/rawdatabase",
        "#/manage/content-ages": "controllers/contentageoptions",
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

    var coreOpenControllers = [
        "controllers/signin",
        "controllers/signup",
        "controllers/magiclink",
        "controllers/confirmemail",
        "controllers/profilepassword"
    ];

    var openControllers = coreOpenControllers.slice();

    var restrictionController = "controllers/restriction";
    var aliasRoute = "#/:slug";
    var siteController = "controllers/site";
    var aliasController = "controllers/routealias";
    var following = false;
    var execution = 0;

    ko.components.register("pager", { require: "components/pager" });
    ko.components.register("captcha", { require: "components/captcha" });
    ko.components.register("two-factor-challenge", { require: "components/twofactorchallenge" });
    ko.components.register("recovery-codes", { require: "components/recoverycodes" });
    ko.components.register("totp-enrollment", { require: "components/totpenrollment" });
    ko.components.register("letter-pager", { require: "components/letterpager" });
    ko.components.register("access-editor", { require: "components/accesseditor" });
    ko.components.register("content-age", { require: "components/contentage" });
    ko.components.register("download-tree", { require: "components/downloadtree" });
    ko.components.register("menu-items", { require: "components/menuitems" });
    ko.components.register("menu-nav", { require: "components/menunav" });
    ko.components.register("carousel-editor", { require: "components/carouseleditor" });
    ko.components.register("site-sections-editor", { require: "components/sectionseditor" });
    ko.components.register("site-widget", { require: "components/sitewidget" });
    ko.components.register("site-header", { require: "components/siteheader" });
    ko.components.register("site-footer", { require: "components/sitefooter" });
    ko.components.register("content-locale", { require: "components/contentlocale" });
    ko.components.register("locale-switch", { require: "components/localeswitch" });

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
            layoutShell.route(window.location.hash || "#/");
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
        var ticket = ++execution;
        contentLocale.clearMedia();
        i18n.useContent(contentLocale.current());
        var controllerName = restriction.restricted() && openControllers.indexOf(requested) < 0
            ? restrictionController
            : requested;

        require([controllerName], function (Controller) {
            if (ticket !== execution) {
                return;
            }
            var guardedContext = $.extend({}, context, {
                params: params,
                renderView: function (view, viewModel) {
                    if (ticket === execution) {
                        context.renderView(view, viewModel);
                    }
                }
            });
            new Controller(guardedContext).exec();
        });
    };

    var ignoreFormSubmissions = function () {
        return false;
    };

    var moduleRouteTable = function () {
        return modules.routes().reduce(function (table, route) {
            table[route.path] = route.controller;

            return table;
        }, {});
    };

    var createRouter = function () {
        openControllers = coreOpenControllers.concat(modules.routes().filter(function (route) {
            return route.open;
        }).map(function (route) {
            return route.controller;
        }));

        return Sammy(function () {
            var app = this;

            app._checkFormSubmission = ignoreFormSubmissions;

            patternRoutes.forEach(function (route) {
                app.get(route.pattern, function () {
                    execute(route.controller, this.params.toHash());
                });
            });

            var tables = [routes, moduleRouteTable(), siteRoutes];
            tables.forEach(function (table) {
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
    };

    var router = null;

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
        var settings = session.isSignedIn() ? usersettings.load() : usersettings.clear();
        return settings.then(function (result) {
            if (result.ok) {
                contentLocale.current(contentLocale.defaultLocale([
                    usersettings.locale(),
                    i18n.remembered()
                ], branding.locale()));
            }
            return result;
        });
    };

    var localizeLinks = function () {
        var media = new WeakMap();
        var update = function () {
            document.querySelectorAll("img[src], a[href]").forEach(function (element) {
                var name = element.tagName.toLowerCase() === "img" ? "src" : "href";
                var value = element.getAttribute(name);
                var previous = media.get(element);
                if (!previous || value !== previous.applied) {
                    previous = { source: value };
                }
                var scope = element.closest("[data-content-locale]");
                var locale = scope ? scope.getAttribute("data-content-locale") || null : contentLocale.current();
                var localized = contentLocale.media(previous.source, locale);
                previous.applied = localized;
                media.set(element, previous);
                if (name === "src" && element.closest(".ql-editor")) {
                    if (localized !== previous.source) {
                        element.setAttribute("data-content-locale-source", previous.source);
                    } else {
                        element.removeAttribute("data-content-locale-source");
                    }
                }
                if (localized !== value) {
                    element.setAttribute(name, localized);
                }
            });
        };
        var observer = new MutationObserver(update);
        observer.observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ["href", "src", "data-content-locale"] });
        contentLocale.current.subscribe(update);
        contentLocale.mediaRevision.subscribe(update);
        update();
    };

    return {
        start: function () {
            applyColorScheme();
            branding.title.subscribe(function (title) {
                document.title = title;
            });

            return about.loadLocales().then(function (result) {
                contentLocale.loadFailed(!result.ok);
                if (result.ok) {
                    contentLocale.locales(result.items);
                }
            }).catch(function () {
                contentLocale.loadFailed(true);
            }).then(function () {
                return Promise.all([
                    branding.load(),
                    modules.load(),
                    accounts.load().catch(function () {})
                ]);
            })
                .then(function () {
                    router = createRouter();
                    i18n.useDefault(branding.locale());

                    return Promise.all([syncUserSettings(), syncRestriction()]);
                })
                .then(function () {
                    i18n.useAccount(usersettings.locale());
                    var defaultLocale = contentLocale.defaultLocale([
                        usersettings.locale(),
                        i18n.remembered()
                    ], branding.locale());
                    contentLocale.current(defaultLocale);
                    i18n.useContent(defaultLocale);
                    contentLocale.current.subscribe(function (locale) {
                        i18n.useContent(locale).then(function () {
                            router.runRoute("get", window.location.hash || "#/");
                        });
                    });
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
                    return layoutShell.start(context).then(function () {
                        localizeLinks();
                        router.run("#/");
                    });
                });
        }
    };
});
