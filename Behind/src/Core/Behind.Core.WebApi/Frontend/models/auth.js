define(["models/http", "models/session"], function (http, session) {
    var genericFailure = "Something went wrong. Please try again.";

    var success = function (message) {
        return { ok: true, message: message };
    };

    var failure = function (response, messages) {
        var details = Array.isArray(response.data) ? response.data.join(" ") : "";

        return { ok: false, message: messages[response.status] || details || genericFailure };
    };

    return {
        signIn: function (email, password) {
            return http.post("/sign/in/password", { email: email, password: password }).then(function (response) {
                if (response.ok) {
                    session.start(response.data);
                    return success("Signed in.");
                }

                return failure(response, { 400: "Enter a valid email and password.", 401: "Invalid email or password." });
            });
        },

        signInWithMagicLink: function (token) {
            if (!token) {
                return Promise.resolve({ ok: false, message: "This sign in link is incomplete." });
            }

            return http.post("/sign/in/magiclink/redeem", { token: token }).then(function (response) {
                if (response.ok) {
                    session.start(response.data);
                    return success("Signed in.");
                }

                return failure(response, {
                    400: "This sign in link is incomplete.",
                    401: "This sign in link is invalid or has expired.",
                    404: "Magic link sign in is not available.",
                    429: "Too many attempts. Please wait before trying again."
                });
            });
        },

        signUp: function (email, password) {
            return http.post("/sign/up/password", { email: email, password: password }).then(function (response) {
                return response.ok
                    ? success("Account created. You can sign in now.")
                    : failure(response, { 409: "This email is already in use." });
            });
        },

        requestMagicLink: function (email) {
            return http.post("/sign/in/magiclink", { email: email }).then(function (response) {
                return response.ok
                    ? success("If the account exists, a sign in link is on its way.")
                    : failure(response, {
                        400: "Enter a valid email.",
                        404: "Magic link sign in is not available.",
                        429: "Too many requests. Please wait before trying again."
                    });
            });
        },

        loadSession: function () {
            return http.get("/session", session.refreshHeaders()).then(function (response) {
                if (response.ok) {
                    if (response.data.refreshed) {
                        session.renew(response.data.refreshed);
                    }

                    session.identify(response.data.user.displayName);

                    return { ok: true, message: "", data: response.data };
                }

                if (response.status === 401) {
                    session.end();
                }

                return failure(response, {
                    401: "Your session has expired. Please sign in again.",
                    409: "Your session was changed elsewhere. Please try again."
                });
            });
        },

        elevate: function (target, code) {
            return http.post("/session/elevate/" + target, { code: code }).then(function (response) {
                return response.ok
                    ? success("Your role was elevated.")
                    : failure(response, {
                        400: "Enter the 6 to 8 digit authenticator code.",
                        401: "Your session has expired. Please sign in again.",
                        403: "The code is not valid.",
                        404: "Administrator elevation is not available.",
                        429: "Too many attempts. Please wait before trying again."
                    });
            });
        },

        signOut: function () {
            return http.post("/sign/out").then(function () {
                session.end();
                return success("Signed out.");
            });
        }
    };
});
