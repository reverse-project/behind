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

    var pages = [
        { name: "Users", icon: "fa-users", href: "#/users", allowed: canManageUsers },
        { name: "Roles", icon: "fa-user-shield", href: "#/roles", allowed: canManageRoles },
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
