define(["models/http", "models/i18n"], function (http, i18n) {
    var result = function (response) {
        return response.ok
            ? { ok: true, data: response.data, message: "" }
            : { ok: false, message: i18n.t(
                response.status === 409
                    ? (response.data && response.data.code === "lastEmail" ? "profile.emailLast" : "profile.emailInUse")
                    : ({
                        400: "profile.emailInvalid",
                        401: "common.error.sessionExpired",
                        403: "profile.emailImpersonated",
                        404: "profile.emailNotFound",
                        429: "auth.tooManyAttempts",
                        503: "profile.emailSendFailed"
                    }[response.status] || "common.error.generic")) };
    };

    return {
        load: function () {
            return http.get("/profile/emails").then(result);
        },
        save: function (id, email) {
            return (id
                ? http.put("/profile/emails/" + encodeURIComponent(id), { email: email })
                : http.post("/profile/emails", { email: email })).then(result);
        },
        remove: function (id) {
            return http.delete("/profile/emails/" + encodeURIComponent(id)).then(result);
        },
        resend: function (id) {
            return http.post("/profile/emails/" + encodeURIComponent(id) + "/confirmation").then(result);
        }
    };
});
