define(["ko", "models/session"], function (ko, session) {
    var canManageUsers = function () {
        return session.hasGrant("User.Manage");
    };

    var pages = [
        { name: "Users", icon: "fa-users", href: "#/users", allowed: canManageUsers }
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
