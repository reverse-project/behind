define(["models/auth", "models/session"], function (auth, session) {
    return function IndexViewModel() {
        this.isSignedIn = session.isSignedIn;
        this.signOut = function () {
            return auth.signOut();
        };
    };
});
