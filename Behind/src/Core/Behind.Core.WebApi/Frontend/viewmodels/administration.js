define(["ko", "models/session", "models/i18n"], function (ko, session, i18n) {
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

    var canReadConfiguration = function () {
        return session.hasGrant("Configuration.Read");
    };

    var pages = [
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
        { name: "administration.pages.auditLog", icon: "fa-clipboard-list", href: "#/audit-log", allowed: canReadAuditLog },
        { name: "administration.pages.settings", icon: "fa-sliders", href: "#/settings", allowed: canReadConfiguration }
    ];

    return function AdministrationViewModel() {
        var self = this;

        self.pages = ko.pureComputed(function () {
            return pages
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
