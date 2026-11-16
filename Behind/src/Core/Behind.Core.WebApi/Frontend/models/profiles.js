define(["models/http", "models/i18n"], function (http, i18n) {
    var result = function (response) {
        return response.ok
            ? { ok: true, data: response.data, message: "" }
            : { ok: false, message: i18n.t({
                400: "profile.invalidDetails",
                401: "common.error.sessionExpired",
                403: "auth.userAction.forbidden",
                404: "auth.user.notFound"
            }[response.status] || "common.error.generic") };
    };

    return {
        get: function (id) {
            return http.get("/users/profiles/" + encodeURIComponent(id)).then(result);
        },
        details: function () {
            return http.get("/profile/details").then(result);
        },
        save: function (birthday, hideEmail) {
            return http.put("/profile/details", { birthday: birthday || null, hideEmail: hideEmail }).then(result);
        }
    };
});
