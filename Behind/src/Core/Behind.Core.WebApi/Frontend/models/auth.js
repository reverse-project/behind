define(["models/http", "models/session", "models/user"], function (http, session, user) {
    var genericFailure = "Something went wrong. Please try again.";

    var success = function (message) {
        return { ok: true, message: message };
    };

    var failure = function (response, messages) {
        var details = Array.isArray(response.data) ? response.data.join(" ") : "";

        return { ok: false, message: messages[response.status] || details || genericFailure };
    };

    var bannedMessage = function (response) {
        var reason = response.data && response.data.reason ? response.data.reason : "";

        return reason ? "This account is banned: " + reason : "This account is banned.";
    };

    var userActionFailures = {
        400: "Enter a message of up to 500 characters.",
        401: "Your session has expired. Please sign in again.",
        403: "You are not allowed to do that to this user.",
        404: "This user no longer exists.",
        409: "This user is referenced by the audit log and cannot be permanently deleted."
    };

    var userAction = function (path, body, done) {
        return http.post("/users/" + path, body).then(function (response) {
            return response.ok ? success(done) : failure(response, userActionFailures);
        });
    };

    var roleActionFailures = {
        400: "The role name or access selection is not valid.",
        401: "Your session has expired. Please sign in again.",
        403: "You are not allowed to change this role.",
        404: "This role no longer exists.",
        409: "A role with this name already exists."
    };

    var userAccessFailures = {
        400: "The access selection is not valid. Every user keeps the Users role.",
        401: "Your session has expired. Please sign in again.",
        403: "You are not allowed to change this user's access.",
        404: "This user no longer exists."
    };

    var loaded = function (response, failures) {
        return response.ok ? { ok: true, message: "", data: response.data } : failure(response, failures);
    };

    var saved = function (response, done, failures) {
        return response.ok ? success(done) : failure(response, failures);
    };

    return {
        signIn: function (email, password) {
            return http.post("/sign/in/password", { email: email, password: password }).then(function (response) {
                if (response.ok) {
                    session.start(response.data);
                    return success("Signed in.");
                }

                return failure(response, {
                    400: "Enter a valid email and password.",
                    401: "Invalid email or password.",
                    403: bannedMessage(response)
                });
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
                    403: bannedMessage(response),
                    404: "Magic link sign in is not available.",
                    429: "Too many attempts. Please wait before trying again."
                });
            });
        },

        signUp: function (email, password, displayName) {
            return http.post("/sign/up/password", { email: email, password: password, displayName: displayName }).then(function (response) {
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

                    session.identify(user.label(response.data.user), user.grants(response.data.user));

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

        changeDisplayName: function (displayName) {
            return http.put("/profile/displayName", { displayName: displayName }).then(function (response) {
                return response.ok
                    ? success("Your display name was saved.")
                    : failure(response, {
                        400: "The display name is too long.",
                        401: "Your session has expired. Please sign in again."
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

        loadSessions: function (paging) {
            var query = "?activePage=" + paging.activePage + "&endedPage=" + paging.endedPage + "&pageSize=" + paging.pageSize;

            return http.get("/sessions" + query).then(function (response) {
                return response.ok
                    ? { ok: true, message: "", data: response.data }
                    : failure(response, { 401: "Your session has expired. Please sign in again." });
            });
        },

        loadUsers: function (query) {
            var url = "/users?page=" + query.page + "&pageSize=" + query.pageSize
                + "&search=" + encodeURIComponent(query.search) + "&letter=" + encodeURIComponent(query.letter)
                + (query.deleted ? "&deleted=true" : "");

            return http.get(url).then(function (response) {
                return response.ok
                    ? { ok: true, message: "", data: response.data }
                    : failure(response, {
                        401: "Your session has expired. Please sign in again.",
                        403: query.deleted
                            ? "You need developer access to see deleted users."
                            : "You need administrator access to manage users."
                    });
            });
        },

        elevateUser: function (userId, role) {
            return userAction(userId + "/elevate/" + role, undefined, "The role was granted.");
        },

        deelevateUser: function (userId, role) {
            return userAction(userId + "/deelevate/" + role, undefined, "The role was removed.");
        },

        loadUserAccess: function (userId) {
            return http.get("/users/" + userId + "/access").then(function (response) {
                return loaded(response, userAccessFailures);
            });
        },

        saveUserAccess: function (userId, access) {
            return http.put("/users/" + userId + "/access", access).then(function (response) {
                return saved(response, "The access was saved.", userAccessFailures);
            });
        },

        loadRoles: function () {
            return http.get("/roles").then(function (response) {
                return loaded(response, {
                    401: "Your session has expired. Please sign in again.",
                    403: "You need role management access to see roles."
                });
            });
        },

        createRole: function (name) {
            return http.post("/roles", { name: name }).then(function (response) {
                return saved(response, "The role was created.", roleActionFailures);
            });
        },

        saveRoleAccess: function (roleId, access) {
            return http.put("/roles/" + roleId + "/access", access).then(function (response) {
                return saved(response, "The role was saved.", roleActionFailures);
            });
        },

        deleteRole: function (roleId) {
            return http.post("/roles/" + roleId + "/delete").then(function (response) {
                return saved(response, "The role was deleted.", roleActionFailures);
            });
        },

        signOutUser: function (userId) {
            return userAction(userId + "/sign/out", undefined, "The user was signed out of all sessions.");
        },

        banUser: function (userId, reason) {
            return userAction(userId + "/ban", { reason: reason }, "The user was banned and signed out of all sessions.");
        },

        unbanUser: function (userId) {
            return userAction(userId + "/unban", undefined, "The user was unbanned.");
        },

        deleteUser: function (userId) {
            return userAction(userId + "/delete", undefined, "The user was deleted.");
        },

        restoreUser: function (userId) {
            return userAction(userId + "/restore", undefined, "The user was restored.");
        },

        hardDeleteUser: function (userId) {
            return userAction(userId + "/hard/delete", undefined, "The user was permanently deleted.");
        },

        loadWallOfShame: function (paging) {
            var query = "?page=" + paging.page + "&pageSize=" + paging.pageSize;

            return http.get("/wallOfShame" + query).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, {});
            });
        },

        loadAuditLog: function (query) {
            var url = "/auditLog?fromDate=" + encodeURIComponent(query.fromDate) + "&toDate=" + encodeURIComponent(query.toDate)
                + "&search=" + encodeURIComponent(query.search) + "&oldestFirst=" + query.oldestFirst
                + "&page=" + query.page + "&pageSize=" + query.pageSize;

            return http.get(url).then(function (response) {
                return loaded(response, {
                    400: "Choose a start date that is not after the end date.",
                    401: "Your session has expired. Please sign in again.",
                    403: "You need audit log access to see the audit log."
                });
            });
        },

        signOutAll: function () {
            return http.post("/sign/out/all").then(function (response) {
                if (response.ok || response.status === 401) {
                    session.end();
                    return success("Signed out of all sessions.");
                }

                return failure(response, {});
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
