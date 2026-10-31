define(["ko", "models/auth", "models/session"], function (ko, auth, session) {
    return function HeaderViewModel(context) {
        var self = this;

        self.isSignedIn = session.isSignedIn;

        self.label = ko.pureComputed(function () {
            return session.displayName() || "Account";
        });

        self.signOut = function () {
            return auth.signOut().then(function () {
                context.navigate("#/");
            });
        };

        var needsIdentity = ko.pureComputed(function () {
            return session.isSignedIn() && session.displayName() === "";
        });

        var identify = function (required) {
            if (required) {
                auth.loadSession();
            }
        };

        needsIdentity.subscribe(identify);
        identify(needsIdentity());
    };
});
