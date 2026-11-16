define(["ko", "models/auth", "models/session", "models/menus", "models/accounts", "models/branding", "models/i18n"], function (ko, auth, session, menus, accounts, branding, i18n) {
    var navigationSequence = 0;

    return function HeaderViewModel(context) {
        var self = this;
        var main = menus.follow("main");

        self.navigationId = "navigation-" + (++navigationSequence);
        var itemsOfKind = function (kind) {
            return ko.pureComputed(function () {
                return main().items.filter(function (item) {
                    return item.kind === kind;
                });
            });
        };

        self.isSignedIn = session.isSignedIn;
        self.canSignIn = accounts.signIn;
        self.canSignUp = accounts.signUp;

        self.title = branding.title;

        self.links = itemsOfKind("Link");

        self.userItems = itemsOfKind("User");

        self.label = ko.pureComputed(function () {
            return session.displayName() || i18n.t("app.account");
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

        var identitySubscription = needsIdentity.subscribe(identify);

        self.dispose = function () {
            identitySubscription.dispose();
            needsIdentity.dispose();
            self.links.dispose();
            self.userItems.dispose();
            self.label.dispose();
            main.dispose();
        };

        identify(session.isSignedIn());
    };
});
