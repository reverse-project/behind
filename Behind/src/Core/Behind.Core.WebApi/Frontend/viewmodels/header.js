define(["ko", "models/auth", "models/session", "models/menus"], function (ko, auth, session, menus) {
    return function HeaderViewModel(context) {
        var self = this;
        var main = menus.follow("main");

        var itemsOfKind = function (kind) {
            return ko.pureComputed(function () {
                return main().items.filter(function (item) {
                    return item.kind === kind;
                });
            });
        };

        self.isSignedIn = session.isSignedIn;

        self.links = itemsOfKind("Link");

        self.userItems = itemsOfKind("User");

        self.label = ko.pureComputed(function () {
            return session.displayName() || "Account";
        });

        self.isImpersonating = session.isImpersonating;

        self.stopImpersonating = function () {
            return auth.stopImpersonating().then(function () {
                context.navigate("#/");
            });
        };

        self.signOut = function () {
            return auth.signOut().then(function () {
                context.navigate("#/");
            });
        };

        var needsIdentity = ko.pureComputed(function () {
            return session.isSignedIn() && (session.displayName() === "" || session.grants() === null);
        });

        var identify = function (required) {
            if (required) {
                auth.loadSession();
            }
        };

        needsIdentity.subscribe(identify);
        identify(session.isSignedIn());
    };
});
