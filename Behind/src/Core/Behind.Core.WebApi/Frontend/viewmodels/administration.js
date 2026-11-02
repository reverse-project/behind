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

    var pages = [
        { name: "Users", icon: "fa-users", href: "#/users", allowed: canManageUsers },
        { name: "Roles", icon: "fa-user-shield", href: "#/roles", allowed: canManageRoles },
        { name: "Sites", icon: "fa-file-lines", href: "#/sites", allowed: canReadSites },
        { name: "News", icon: "fa-newspaper", href: "#/manage/news", allowed: canReadNews },
        { name: "News categories", icon: "fa-tags", href: "#/manage/news-categories", allowed: canReadNews },
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
