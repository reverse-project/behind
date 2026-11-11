define(["models/http", "models/i18n"], function (http, i18n) {
    var checked = function (response) {
        if (!response.ok) {
            var key = response.status === 401 ? "common.error.sessionExpired"
                : response.status === 403 ? "forum.denied"
                    : response.status === 409 ? "forum.conflict" : "common.error.generic";

            throw new Error(i18n.t(key));
        }

        return response.data;
    };

    return {
        get: function (path) {
            return http.get("/forum/" + path).then(checked);
        },
        save: function (path, body) {
            return http.post("/forum/" + path, body).then(checked);
        }
    };
});
