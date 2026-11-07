define(["ko", "models/session", "models/accounts", "models/restriction", "models/i18n"], function (ko, session, accounts, restriction, i18n) {
    return function RestrictionViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canSignIn = ko.pureComputed(function () {
            return !session.isSignedIn() && accounts.signIn();
        });
        self.canSignUp = ko.pureComputed(function () {
            return !session.isSignedIn() && accounts.signUp();
        });
        self.title = ko.pureComputed(function () {
            return restriction.title() || i18n.t("restriction.title");
        });
        self.message = ko.pureComputed(function () {
            return restriction.message() || i18n.t("restriction.defaultMessage");
        });
    };
});
