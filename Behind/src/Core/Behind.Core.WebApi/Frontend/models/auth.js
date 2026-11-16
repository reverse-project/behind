define(["models/http", "models/session", "models/user", "models/i18n", "models/webauthn"], function (http, session, user, i18n, webauthn) {
    var genericFailure = "common.error.generic";
    var sessionExpired = "common.error.sessionExpired";

    var success = function (message) {
        return { ok: true, message: i18n.t(message) };
    };

    var captchaRejected = function (response) {
        return response.status === 400 && !!response.data && response.data.code === "captchaInvalid";
    };

    var failureMessage = function (response, messages) {
        if (captchaRejected(response)) {
            return i18n.t("auth.captcha.invalid");
        }

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
        if (response.data && response.data.code === "twoFactorUnavailable") {
            return i18n.t("auth.twoFactor.unavailable");
        }

        if (response.data && response.data.code === "emailNotConfirmed") {
            return i18n.t("auth.emailNotConfirmed");
        }

        if (response.data && response.data.code === "approvalPending") {
            return i18n.t("auth.approvalPending");
        }

        var reason = response.data && response.data.reason ? response.data.reason : "";

        return reason ? i18n.t("auth.bannedReason", [i18n.banReason(reason)]) : i18n.t("auth.banned");
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

    var signedIn = function (data) {
        if (data && data.resetToken && !data.accessToken) {
            return {
                ok: true,
                message: "",
                lostPassword: true,
                passwordReset: { token: data.resetToken, expires: data.expires },
                recoveryCodes: data.recoveryCodes || []
            };
        }

        var result = success("auth.signIn.done");

        session.start(data);
        result.lostPassword = data.function === "LostPassword";
        result.recoveryCodes = data.recoveryCodes || [];

        return result;
    };

    var isChallenge = function (data) {
        return !!data && !!data.ticket && !data.accessToken;
    };

    var challengeOf = function (data) {
        return {
            ticket: data.ticket,
            methods: data.methods || [],
            setupRequired: data.setupRequired === true,
            function: data.function || null
        };
    };

    var completed = function (response) {
        return isChallenge(response.data)
            ? { ok: true, message: "", twoFactor: challengeOf(response.data) }
            : signedIn(response.data);
    };

    var generic = function () {
        return i18n.t(genericFailure);
    };

    var messageByCode = function (messages, otherwise) {
        return function (response) {
            var key = response.data ? messages[response.data.code] : undefined;

            return key ? i18n.t(key) : otherwise(response);
        };
    };

    var enrollment = function (data) {
        return {
            secret: data.secret,
            uri: data.uri,
            image: "data:image/svg+xml;charset=utf-8," + encodeURIComponent(data.qrCode)
        };
    };

    var enrollmentResult = function (response, failed) {
        return response.ok ? { ok: true, message: "", data: enrollment(response.data) } : failed(response);
    };

    var optionalName = function (name) {
        var trimmed = (name || "").trim();

        return trimmed === "" ? undefined : trimmed;
    };

    var signInStepFailures = {
        400: "auth.twoFactor.invalid",
        401: "auth.twoFactor.ticketInvalid",
        403: messageByCode({ invalidCode: "auth.twoFactor.invalidCode" }, bannedMessage),
        404: "auth.twoFactor.unavailable",
        429: "auth.tooManyAttempts"
    };

    var stepFailed = function (response, overrides) {
        var result = failure(response, Object.assign({}, signInStepFailures, overrides));

        result.restart = response.status === 401;

        return result;
    };

    var signInStep = function (path, body, overrides) {
        return http.post("/sign/in/two-factor/" + path, body).then(function (response) {
            return response.ok ? signedIn(response.data) : stepFailed(response, overrides);
        });
    };

    var twoFactorFailures = {
        400: "auth.twoFactor.invalid",
        401: messageByCode({ ticketInvalid: "auth.twoFactor.registrationExpired" }, function () {
            return i18n.t(sessionExpired);
        }),
        403: messageByCode({ invalidCode: "auth.twoFactor.invalidCode" }, function () {
            return i18n.t("auth.twoFactor.impersonated");
        }),
        404: "auth.twoFactor.notFound",
        409: messageByCode({
            alreadyEnabled: "auth.twoFactor.alreadyEnabled",
            required: "auth.twoFactor.required",
            notEnabled: "auth.twoFactor.notEnabled"
        }, generic)
    };

    var withRecoveryCodes = function (response, done) {
        if (!response.ok) {
            return failure(response, twoFactorFailures);
        }

        var result = success(done);

        result.recoveryCodes = response.data && response.data.recoveryCodes ? response.data.recoveryCodes : [];

        return result;
    };

    var passkeyCeremony = function (optionsUrl, optionsBody, browserStep, submit, failed) {
        return http.post(optionsUrl, optionsBody).then(function (response) {
            if (!response.ok) {
                return failed(response);
            }

            var ticket = response.data.ticket;

            return browserStep(response.data.options).then(function (credential) {
                if (!credential.ok) {
                    return { ok: false, message: credential.message, ticket: ticket };
                }

                return submit(ticket, credential.credential).then(function (result) {
                    return Object.assign({ ticket: ticket }, result);
                });
            });
        });
    };

    var twoFactor = {
        emailAddresses: function (ticket) {
            return http.post("/sign/in/two-factor/email/addresses", { ticket: ticket }).then(function (response) {
                return response.ok
                    ? { ok: true, message: "", data: response.data }
                    : stepFailed(response);
            });
        },

        sendEmail: function (ticket, addressId) {
            return http.post("/sign/in/two-factor/email", { ticket: ticket, addressId: addressId }).then(function (response) {
                return response.ok
                    ? Object.assign(success("auth.twoFactor.emailSent"), { ticket: response.data.ticket })
                    : stepFailed(response, { 503: "auth.twoFactor.emailDeliveryFailed" });
            });
        },

        verifyEmail: function (ticket, code) {
            return signInStep("email/verify", { ticket: ticket, code: code });
        },

        verifyTotp: function (ticket, code) {
            return signInStep("totp", { ticket: ticket, code: code });
        },

        verifyRecovery: function (ticket, code) {
            return signInStep("recovery", { ticket: ticket, code: code });
        },

        signInWithPasskey: function (ticket) {
            return passkeyCeremony(
                "/sign/in/two-factor/passkey/options",
                { ticket: ticket },
                webauthn.get,
                function (next, credential) {
                    return signInStep("passkey", { ticket: next, credential: credential });
                },
                function (response) {
                    return stepFailed(response, { 404: "auth.twoFactor.noPasskey" });
                }
            );
        },

        setupTotp: function (ticket) {
            return http.post("/sign/in/two-factor/setup/totp", { ticket: ticket }).then(function (response) {
                return enrollmentResult(response, function (failed) {
                    return stepFailed(failed, { 409: "auth.twoFactor.alreadyEnabled" });
                });
            });
        },

        confirmSetupTotp: function (ticket, code) {
            return signInStep("setup/totp/confirm", { ticket: ticket, code: code });
        },

        setupPasskey: function (ticket, name) {
            return passkeyCeremony(
                "/sign/in/two-factor/setup/passkey/options",
                { ticket: ticket },
                webauthn.create,
                function (next, credential) {
                    return signInStep("setup/passkey", { ticket: next, credential: credential, name: optionalName(name) });
                },
                function (response) {
                    return stepFailed(response);
                }
            );
        },

        load: function () {
            return http.get("/profile/two-factor").then(function (response) {
                return loaded(response, twoFactorFailures);
            });
        },

        startTotp: function () {
            return http.post("/profile/two-factor/totp").then(function (response) {
                return enrollmentResult(response, function (failed) {
                    return failure(failed, twoFactorFailures);
                });
            });
        },

        confirmTotp: function (code) {
            return http.post("/profile/two-factor/totp/confirm", { code: code }).then(function (response) {
                return withRecoveryCodes(response, "auth.twoFactor.totpEnabled");
            });
        },

        removeTotp: function () {
            return http.delete("/profile/two-factor/totp").then(function (response) {
                return saved(response, "auth.twoFactor.totpRemoved", twoFactorFailures);
            });
        },

        addPasskey: function (name) {
            return passkeyCeremony(
                "/profile/two-factor/passkeys/options",
                undefined,
                webauthn.create,
                function (ticket, credential) {
                    return http.post("/profile/two-factor/passkeys", { ticket: ticket, credential: credential, name: optionalName(name) }).then(function (response) {
                        return withRecoveryCodes(response, "auth.twoFactor.passkeyAdded");
                    });
                },
                function (response) {
                    return failure(response, twoFactorFailures);
                }
            );
        },

        removePasskey: function (id) {
            return http.delete("/profile/two-factor/passkeys/" + encodeURIComponent(id)).then(function (response) {
                return saved(response, "auth.twoFactor.passkeyRemoved", twoFactorFailures);
            });
        },

        regenerateRecoveryCodes: function () {
            return http.post("/profile/two-factor/recovery-codes").then(function (response) {
                return withRecoveryCodes(response, "auth.twoFactor.codesGenerated");
            });
        }
    };

    return {
        twoFactor: twoFactor,

        signInWithPasskey: function (email, captcha) {
            if (!email || !email.trim()) {
                return incomplete("auth.passkey.invalidEmail");
            }

            var failed = function (response) {
                return failure(response, {
                    400: "auth.passkey.invalidEmail",
                    401: "auth.passkey.invalid",
                    403: bannedMessage,
                    404: "auth.passkey.unavailable",
                    429: "auth.tooManyAttempts"
                });
            };

            return passkeyCeremony(
                "/sign/in/passkey/options",
                { email: email, captcha: captcha },
                webauthn.get,
                function (ticket, credential) {
                    return http.post("/sign/in/passkey", { ticket: ticket, credential: credential }).then(function (response) {
                        return response.ok ? completed(response) : failed(response);
                    });
                },
                failed
            );
        },

        signIn: function (email, password, captcha) {
            return http.post("/sign/in/password", { email: email, password: password, captcha: captcha }).then(function (response) {
                if (response.ok) {
                    return completed(response);
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
                    return completed(response);
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

        resetPassword: function (token, password) {
            if (!token) {
                return incomplete("auth.passwordReset.expired");
            }

            return http.post("/sign/in/password/reset", { token: token, password: password }).then(function (response) {
                return response.ok
                    ? success("auth.passwordReset.done")
                    : failure(response, {
                        401: "auth.passwordReset.expired",
                        404: "auth.passwordReset.unavailable",
                        429: "auth.tooManyAttempts"
                    });
            });
        },

        signUp: function (email, password, displayName, captcha) {
            return http.post("/sign/up/password", { email: email, password: password, displayName: displayName, captcha: captcha }).then(function (response) {
                return response.ok
                    ? success(response.data && response.data.code === "approvalPending" ? "auth.signUp.pendingApproval" : "auth.signUp.done")
                    : failure(response, { 409: "auth.signUp.emailInUse" });
            });
        },

        confirmEmail: function (userId, token, emailId) {
            if (!userId || !token) {
                return incomplete("auth.confirmEmail.incomplete");
            }

            return http.post("/sign/up/confirm", { userId: userId, token: token, emailId: emailId }).then(function (response) {
                return response.ok
                    ? success("auth.confirmEmail.done")
                    : failure(response, {
                        400: "auth.confirmEmail.invalid"
                    });
            });
        },

        resendConfirmation: function (email, captcha) {
            return http.post("/sign/up/confirm/resend", { email: email, captcha: captcha }).then(function (response) {
                return response.ok
                    ? success("auth.resendConfirmation.requested")
                    : failure(response, {
                        400: "auth.resendConfirmation.invalidEmail",
                        404: "auth.resendConfirmation.unavailable",
                        429: "auth.resendConfirmation.tooManyRequests"
                    });
            });
        },

        requestMagicLink: function (email, captcha, done, magicLinkFunction) {
            return http.post("/sign/in/magiclink", { email: email, function: magicLinkFunction || "SignIn", captcha: captcha }).then(function (response) {
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
                    session.confirmImpersonation(!!response.data.impersonator);

                    return { ok: true, message: "", data: response.data };
                }

                session.confirmImpersonation();

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

        resetRoleGrants: function (roleId) {
            return http.post("/roles/" + roleId + "/reset-grants").then(function (response) {
                return saved(response, "auth.role.grantsReset", roleActionFailures);
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
                + "&entity=" + encodeURIComponent(query.entity) + "&action=" + encodeURIComponent(query.action)
                + "&entityExact=true"
                + "&actor=" + encodeURIComponent(query.actor) + "&behalfOf=" + encodeURIComponent(query.behalfOf)
                + "&sortBy=" + encodeURIComponent(query.sortBy) + "&descending=" + query.descending
                + "&page=" + query.page + "&pageSize=" + query.pageSize;

            return http.get(url).then(function (response) {
                return loaded(response, {
                    400: "auth.auditLog.invalidRange",
                    401: sessionExpired,
                    403: "auth.auditLog.forbidden"
                });
            });
        },

        loadAuditEntityTypes: function () {
            return http.get("/auditLog/entityTypes").then(function (response) {
                return loaded(response, {
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
