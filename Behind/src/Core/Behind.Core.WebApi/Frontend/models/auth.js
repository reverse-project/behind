define(["models/http", "models/session", "models/user", "models/i18n"], function (http, session, user, i18n) {
    var genericFailure = "common.error.generic";
    var sessionExpired = "common.error.sessionExpired";

    var success = function (message) {
        return { ok: true, message: i18n.t(message) };
    };

    var failureMessage = function (response, messages) {
        var found = messages[response.status];

        if (typeof found === "function") {
            return found(response);
        }

        if (found) {
            return i18n.t(found);
        }

        var details = Array.isArray(response.data) ? response.data.join(" ") : "";

        return details || i18n.t(genericFailure);
    };

    var failure = function (response, messages) {
        return { ok: false, message: failureMessage(response, messages) };
    };

    var incomplete = function (message) {
        return Promise.resolve({ ok: false, message: i18n.t(message) });
    };

    var bannedMessage = function (response) {
        if (response.data && response.data.code === "emailNotConfirmed") {
            return i18n.t("auth.emailNotConfirmed");
        }

        var reason = response.data && response.data.reason ? response.data.reason : "";

        return reason ? i18n.t("auth.bannedReason", [reason]) : i18n.t("auth.banned");
    };

    var userActionFailures = {
        400: "auth.userAction.invalidMessage",
        401: sessionExpired,
        403: "auth.userAction.forbidden",
        404: "auth.user.notFound",
        409: "auth.userAction.referenced"
    };

    var userAction = function (path, body, done) {
        return http.post("/users/" + path, body).then(function (response) {
            return response.ok ? success(done) : failure(response, userActionFailures);
        });
    };

    var roleActionFailures = {
        400: "auth.roleAction.invalid",
        401: sessionExpired,
        403: "auth.roleAction.forbidden",
        404: "auth.roleAction.notFound",
        409: "auth.roleAction.nameTaken"
    };

    var roleDeveloperFailures = {
        401: sessionExpired,
        403: "auth.roleAction.forbidden",
        404: "auth.roleAction.notFound",
        409: "auth.roleAction.stillDeveloper"
    };

    var roleDeletedFailures = {
        401: sessionExpired,
        403: "auth.roleDeleted.forbidden",
        404: "auth.roleDeleted.notDeleted",
        409: "auth.roleDeleted.referenced"
    };

    var userAccessFailures = {
        400: "auth.userAccess.invalid",
        401: sessionExpired,
        403: "auth.userAccess.forbidden",
        404: "auth.user.notFound"
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
                    var signedIn = success("auth.signIn.done");

                    session.start(response.data);
                    signedIn.lostPassword = response.data.function === "LostPassword";

                    return signedIn;
                }

                return failure(response, {
                    400: "auth.signIn.invalid",
                    401: "auth.signIn.badCredentials",
                    403: bannedMessage
                });
            });
        },

        signInWithMagicLink: function (token) {
            if (!token) {
                return incomplete("auth.magicLink.incomplete");
            }

            return http.post("/sign/in/magiclink/redeem", { token: token }).then(function (response) {
                if (response.ok) {
                    var signedIn = success("auth.signIn.done");

                    session.start(response.data);
                    signedIn.lostPassword = response.data.function === "LostPassword";

                    return signedIn;
                }

                var result = failure(response, {
                    400: "auth.magicLink.incomplete",
                    401: "auth.magicLink.invalid",
                    403: bannedMessage,
                    404: "auth.magicLink.unavailable",
                    429: "auth.tooManyAttempts"
                });

                result.emailNotConfirmed = response.status === 403 && !!response.data && response.data.code === "emailNotConfirmed";

                return result;
            });
        },

        signUp: function (email, password, displayName) {
            return http.post("/sign/up/password", { email: email, password: password, displayName: displayName }).then(function (response) {
                return response.ok
                    ? success("auth.signUp.done")
                    : failure(response, { 409: "auth.signUp.emailInUse" });
            });
        },

        confirmEmail: function (userId, token) {
            if (!userId || !token) {
                return incomplete("auth.confirmEmail.incomplete");
            }

            return http.post("/sign/up/confirm", { userId: userId, token: token }).then(function (response) {
                return response.ok
                    ? success("auth.confirmEmail.done")
                    : failure(response, {
                        400: "auth.confirmEmail.invalid"
                    });
            });
        },

        resendConfirmation: function (email) {
            return http.post("/sign/up/confirm/resend", { email: email }).then(function (response) {
                return response.ok
                    ? success("auth.resendConfirmation.requested")
                    : failure(response, {
                        400: "auth.resendConfirmation.invalidEmail",
                        404: "auth.resendConfirmation.unavailable",
                        429: "auth.resendConfirmation.tooManyRequests"
                    });
            });
        },

        requestMagicLink: function (email, done, magicLinkFunction) {
            return http.post("/sign/in/magiclink", { email: email, function: magicLinkFunction || "SignIn" }).then(function (response) {
                return response.ok
                    ? success(done || "auth.magicLink.requested")
                    : failure(response, {
                        400: "auth.magicLink.invalidEmail",
                        404: "auth.magicLink.unavailable",
                        429: "auth.magicLink.tooManyRequests"
                    });
            });
        },

        loadSession: function () {
            return http.loadSession().then(function (response) {
                if (response.ok) {
                    session.identify(user.label(response.data.user), user.grants(response.data.user));

                    return { ok: true, message: "", data: response.data };
                }

                return failure(response, {
                    401: sessionExpired,
                    409: "auth.session.changed"
                });
            });
        },

        changeDisplayName: function (displayName) {
            return http.put("/profile/displayName", { displayName: displayName }).then(function (response) {
                return response.ok
                    ? success("auth.profile.saved")
                    : failure(response, {
                        400: "auth.profile.tooLong",
                        401: sessionExpired
                    });
            });
        },

        changePassword: function (password) {
            return http.put("/profile/password", { password: password }).then(function (response) {
                return response.ok
                    ? success("auth.password.saved")
                    : failure(response, {
                        400: "auth.password.rejected",
                        401: sessionExpired,
                        403: "auth.password.impersonated"
                    });
            });
        },

        elevate: function (target, code) {
            return http.post("/session/elevate/" + target, { code: code }).then(function (response) {
                return response.ok
                    ? success("auth.elevate.done")
                    : failure(response, {
                        400: "auth.elevate.invalidCode",
                        401: sessionExpired,
                        403: "auth.elevate.wrongCode",
                        404: "auth.elevate.unavailable",
                        429: "auth.tooManyAttempts"
                    });
            });
        },

        loadSessions: function (paging) {
            var query = "?activePage=" + paging.activePage + "&endedPage=" + paging.endedPage + "&pageSize=" + paging.pageSize;

            return http.get("/sessions" + query).then(function (response) {
                return response.ok
                    ? { ok: true, message: "", data: response.data }
                    : failure(response, { 401: sessionExpired });
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
                        401: sessionExpired,
                        403: query.deleted
                            ? "auth.users.needDeveloperDeleted"
                            : "auth.users.needAdministrator"
                    });
            });
        },

        elevateUser: function (userId, role) {
            return userAction(userId + "/elevate/" + role, undefined, "auth.user.roleGranted");
        },

        deelevateUser: function (userId, role) {
            return userAction(userId + "/deelevate/" + role, undefined, "auth.user.roleRemoved");
        },

        loadUserAccess: function (userId) {
            return http.get("/users/" + userId + "/access").then(function (response) {
                return loaded(response, userAccessFailures);
            });
        },

        saveUserAccess: function (userId, access) {
            return http.put("/users/" + userId + "/access", access).then(function (response) {
                return saved(response, "auth.userAccess.saved", userAccessFailures);
            });
        },

        loadRoles: function (query) {
            var url = "/roles?page=" + query.page + "&pageSize=" + query.pageSize
                + "&search=" + encodeURIComponent(query.search) + "&letter=" + encodeURIComponent(query.letter)
                + (query.deleted ? "&deleted=true" : "");

            return http.get(url).then(function (response) {
                return loaded(response, {
                    401: sessionExpired,
                    403: query.deleted
                        ? "auth.roles.needDeveloperDeleted"
                        : "auth.roles.needManagement"
                });
            });
        },

        createRole: function (name, isDeveloper) {
            return http.post("/roles", { name: name, isDeveloper: isDeveloper === true }).then(function (response) {
                return saved(response, "auth.role.created", roleActionFailures);
            });
        },

        setRoleDeveloper: function (roleId, isDeveloper) {
            return http.put("/roles/" + roleId + "/developer", { isDeveloper: isDeveloper }).then(function (response) {
                return saved(response, "auth.role.saved", roleDeveloperFailures);
            });
        },

        saveRoleAccess: function (roleId, access) {
            return http.put("/roles/" + roleId + "/access", access).then(function (response) {
                return saved(response, "auth.role.saved", roleActionFailures);
            });
        },

        deleteRole: function (roleId) {
            return http.post("/roles/" + roleId + "/delete").then(function (response) {
                return saved(response, "auth.role.deleted", roleActionFailures);
            });
        },

        restoreRole: function (roleId) {
            return http.post("/roles/" + roleId + "/restore").then(function (response) {
                return saved(response, "auth.role.restored", roleDeletedFailures);
            });
        },

        hardDeleteRole: function (roleId) {
            return http.post("/roles/" + roleId + "/hard/delete").then(function (response) {
                return saved(response, "auth.role.permanentlyDeleted", roleDeletedFailures);
            });
        },

        impersonateUser: function (userId) {
            return http.post("/users/" + userId + "/impersonate").then(function (response) {
                if (response.ok) {
                    session.impersonate(response.data);
                    return success("auth.impersonate.done");
                }

                return failure(response, {
                    401: sessionExpired,
                    403: "auth.impersonate.forbidden",
                    404: "auth.user.notFound"
                });
            });
        },

        stopImpersonating: function () {
            return http.post("/impersonation/stop").then(function () {
                session.stopImpersonating();
                return success("auth.impersonate.stopped");
            });
        },

        signOutUser: function (userId) {
            return userAction(userId + "/sign/out", undefined, "auth.user.signedOut");
        },

        confirmUserEmail: function (userId) {
            return userAction(userId + "/confirm/email", undefined, "auth.user.emailConfirmed");
        },

        banUser: function (userId, reason) {
            return userAction(userId + "/ban", { reason: reason }, "auth.user.banned");
        },

        unbanUser: function (userId) {
            return userAction(userId + "/unban", undefined, "auth.user.unbanned");
        },

        deleteUser: function (userId) {
            return userAction(userId + "/delete", undefined, "auth.user.deleted");
        },

        restoreUser: function (userId) {
            return userAction(userId + "/restore", undefined, "auth.user.restored");
        },

        hardDeleteUser: function (userId) {
            return userAction(userId + "/hard/delete", undefined, "auth.user.permanentlyDeleted");
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
                    400: "auth.auditLog.invalidRange",
                    401: sessionExpired,
                    403: "auth.auditLog.forbidden"
                });
            });
        },

        signOutAll: function () {
            return http.post("/sign/out/all").then(function (response) {
                if (response.ok || response.status === 401) {
                    session.end();
                    return success("auth.signOutAll.done");
                }

                return failure(response, {});
            });
        },

        signOut: function () {
            return http.post("/sign/out").then(function () {
                session.end();
                return success("auth.signOut.done");
            });
        }
    };
});
