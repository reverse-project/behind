define(["models/http", "models/i18n"], function (http, i18n) {
    var genericFailure = "settings.error.generic";
    var readOnlyCode = "configuration_read_only";

    var failures = {
        400: "settings.error.400",
        401: "settings.error.401",
        403: "settings.error.403",
        404: "settings.error.404"
    };

    var readOnlyMessage = "settings.error.readOnly";

    var success = function (message, data) {
        return { ok: true, message: message, data: data };
    };

    var failure = function (response) {
        if (response.status === 403 && response.data && response.data.code === readOnlyCode) {
            return { ok: false, message: i18n.t(readOnlyMessage) };
        }

        return { ok: false, message: i18n.t(failures[response.status] || genericFailure) };
    };

    var filePath = function (file, action) {
        return "/settings/" + encodeURIComponent(file) + (action ? "/" + action : "");
    };

    var toBase64Url = function (text) {
        return window.btoa(unescape(encodeURIComponent(text)))
            .replace(/\+/g, "-")
            .replace(/\//g, "_")
            .replace(/=+$/, "");
    };

    var action = function (file, name, fieldPath, done) {
        return http.post(filePath(file, name), { path: fieldPath }).then(function (response) {
            return response.ok ? success(done, response.data) : failure(response);
        });
    };

    return {
        load: function () {
            return http.get("/settings").then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        save: function (file, values) {
            return http.put(filePath(file), { values: values }).then(function (response) {
                return response.ok
                    ? success(i18n.t("settings.saved"))
                    : failure(response);
            });
        },

        generate: function (file, fieldPath) {
            return action(file, "generate", fieldPath, i18n.t("settings.generated"));
        },

        rotate: function (file, fieldPath) {
            return action(file, "rotate", fieldPath, i18n.t("settings.rotated"));
        },

        loadEmail: function () {
            return http.get("/settings/email").then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        saveEmail: function (request) {
            return http.put("/settings/email", request).then(function (response) {
                return response.ok
                    ? success(i18n.t("settings.emailSaved"))
                    : failure(response);
            });
        },

        loadLogging: function (file) {
            return http.get(filePath(file, "logging")).then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        loadOpenTelemetry: function (file) {
            return http.get(filePath(file, "opentelemetry")).then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        saveAppSettings: function (file, request) {
            return http.put(filePath(file), request).then(function (response) {
                return response.ok
                    ? success(i18n.t("settings.appSettingsSaved"))
                    : failure(response);
            });
        },

        loadQrCode: function (uri) {
            return http.get("/generator/qrcode/" + toBase64Url(uri)).then(function (response) {
                return response.ok && typeof response.data === "string" ? response.data : "";
            });
        }
    };
});
