define(["ko", "models/session"], function (ko, session) {
    var canManageUsers = function () {
        return session.hasGrant("User.Manage");
    };

    var canManageRoles = function () {
        return session.hasGrant("Role.Manage");
    };

    var pages = [
        { name: "Users", icon: "fa-users", href: "#/users", allowed: canManageUsers },
        { name: "Roles", icon: "fa-user-shield", href: "#/roles", allowed: canManageRoles }
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
