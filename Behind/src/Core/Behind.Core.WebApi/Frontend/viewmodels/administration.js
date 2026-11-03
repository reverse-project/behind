define(["ko", "models/session"], function (ko, session) {
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

    var canReadMenus = function () {
        return session.hasGrant("Menu.Read");
    };

    var pages = [
        { name: "Users", icon: "fa-users", href: "#/users", allowed: canManageUsers },
        { name: "Roles", icon: "fa-user-shield", href: "#/roles", allowed: canManageRoles },
        { name: "Sites", icon: "fa-file-lines", href: "#/sites", allowed: canReadSites },
        { name: "News", icon: "fa-newspaper", href: "#/manage/news", allowed: canReadNews },
        { name: "News categories", icon: "fa-tags", href: "#/manage/news-categories", allowed: canReadNews },
        { name: "Articles", icon: "fa-book-open", href: "#/manage/articles", allowed: canReadArticles },
        { name: "Article categories", icon: "fa-tags", href: "#/manage/article-categories", allowed: canReadArticles },
        { name: "Galleries", icon: "fa-images", href: "#/manage/galleries", allowed: canReadGalleries },
        { name: "Menus", icon: "fa-bars", href: "#/manage/menus", allowed: canReadMenus },
        { name: "Audit log", icon: "fa-clipboard-list", href: "#/audit-log", allowed: canReadAuditLog }
    ];

    return function AdministrationViewModel() {
        var self = this;

        self.pages = ko.pureComputed(function () {
            return pages.filter(function (page) {
                return page.allowed();
            });
        });

        self.isEmpty = ko.pureComputed(function () {
            return self.pages().length === 0;
        });
    };
});
