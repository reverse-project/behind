define(["models/http", "models/i18n"], function (http, i18n) {
    var genericFailure = "settings.error.generic";
    var readOnlyCode = "configuration_read_only";
    var failures = {
        400: "settings.error.400",
        401: "settings.error.401",
        403: "settings.error.403",
        404: "settings.error.404",
        409: "users.serviceUsers.conflict"
    };

    var success = function (message, data) {
        return { ok: true, message: message, data: data };
    };

    var failure = function (response) {
        if (response.status === 403 && response.data && response.data.code === readOnlyCode) {
            return { ok: false, message: i18n.t("settings.error.readOnly") };
        }

        return { ok: false, message: i18n.t(failures[response.status] || genericFailure) };
    };

    return {
        load: function (deleted) {
            return http.get("/service-users?deleted=" + (deleted ? "true" : "false")).then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        action: function (action, id, request) {
            var path = "/service-users" + (id ? "/" + encodeURIComponent(id) : "");
            if (action !== "create" && action !== "update") {
                path += "/" + action;
            }
            return (action === "update" ? http.put(path, request) : http.post(path, request)).then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        loadAccess: function (id) {
            return http.get("/service-users/" + encodeURIComponent(id) + "/access").then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        saveAccess: function (id, request) {
            return http.put("/service-users/" + encodeURIComponent(id) + "/access", request).then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        }
    };
});
