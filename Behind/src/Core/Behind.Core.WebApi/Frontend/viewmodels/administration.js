define(["ko", "models/session", "models/i18n", "models/modules"], function (ko, session, i18n, modules) {
    var canManageUsers = function () {
        return session.hasGrant("User.Manage");
    };

    var canManageRoles = function () {
        return session.hasGrant("Role.Manage");
    };

    var canReadAuditLog = function () {
        return session.hasGrant("AuditLog.Read");
    };

    var canReadSites = function () {
        return session.hasGrant("Site.Read");
    };

    var canReadNews = function () {
        return session.hasGrant("News.Read");
    };

    var canReadArticles = function () {
        return session.hasGrant("Article.Read");
    };

    var canReadGalleries = function () {
        return session.hasGrant("Gallery.Read");
    };

    var canReadDownloads = function () {
        return session.hasGrant("Download.Read");
    };

    var canReadMenus = function () {
        return session.hasGrant("Menu.Read");
    };

    var canReadRouteAliases = function () {
        return session.hasGrant("RouteAlias.Read");
    };

    var canReadAccessSettings = function () {
        return session.hasGrant("Access.Read");
    };

    var canManageRawDatabase = function () {
        return session.hasGrant("RawDatabase.Manage");
    };

    var canReadConfiguration = function () {
        return session.hasGrant("Configuration.Read");
    };

    var corePages = [
        { name: "administration.pages.users", icon: "fa-users", href: "#/users", allowed: canManageUsers },
        { name: "administration.pages.roles", icon: "fa-user-shield", href: "#/roles", allowed: canManageRoles },
        { name: "administration.pages.sites", icon: "fa-file-lines", href: "#/sites", allowed: canReadSites },
        { name: "administration.pages.news", icon: "fa-newspaper", href: "#/manage/news", allowed: canReadNews },
        { name: "administration.pages.newsCategories", icon: "fa-tags", href: "#/manage/news-categories", allowed: canReadNews },
        { name: "administration.pages.articles", icon: "fa-book-open", href: "#/manage/articles", allowed: canReadArticles },
        { name: "administration.pages.articleCategories", icon: "fa-tags", href: "#/manage/article-categories", allowed: canReadArticles },
        { name: "administration.pages.galleries", icon: "fa-images", href: "#/manage/galleries", allowed: canReadGalleries },
        { name: "administration.pages.downloads", icon: "fa-download", href: "#/manage/downloads", allowed: canReadDownloads },
        { name: "administration.pages.menus", icon: "fa-bars", href: "#/manage/menus", allowed: canReadMenus },
        { name: "layouts.title", icon: "fa-table-cells", href: "#/manage/layouts", allowed: canReadConfiguration },
        { name: "administration.pages.routeAliases", icon: "fa-route", href: "#/manage/route-aliases", allowed: canReadRouteAliases },
        { name: "administration.pages.accessSettings", icon: "fa-lock", href: "#/manage/access-settings", allowed: canReadAccessSettings },
        { name: "administration.pages.auditLog", icon: "fa-clipboard-list", href: "#/audit-log", allowed: canReadAuditLog },
        { name: "administration.pages.rawDatabase", icon: "fa-database", href: "#/manage/raw-database", allowed: canManageRawDatabase },
        { name: "contentRestrictions.title", icon: "fa-lock", href: "#/manage/content-restrictions", allowed: canManageRawDatabase },
        { name: "administration.pages.settings", icon: "fa-sliders", href: "#/settings", allowed: canReadConfiguration }
    ];

    var modulePages = function () {
        return modules.administrationPages().map(function (page) {
            return {
                name: page.name,
                icon: page.icon,
                href: page.href,
                allowed: function () {
                    return session.hasGrant(page.grant);
                }
            };
        });
    };

    return function AdministrationViewModel() {
        var self = this;

        self.pages = ko.pureComputed(function () {
            return corePages.concat(modulePages())
                .filter(function (page) {
                    return page.allowed();
                })
                .map(function (page) {
                    return { name: i18n.t(page.name), icon: page.icon, href: page.href };
                });
        });

        self.isEmpty = ko.pureComputed(function () {
            return self.pages().length === 0;
        });
    };
});
