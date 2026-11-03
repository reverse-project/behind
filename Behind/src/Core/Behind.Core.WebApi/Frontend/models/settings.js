define(["models/http"], function (http) {
    var genericFailure = "Something went wrong. Please try again.";
    var readOnlyCode = "configuration_read_only";

    var failures = {
        400: "One of the values is not valid.",
        401: "Your session has expired. Please sign in again.",
        403: "You need configuration management access to do that.",
        404: "This file or setting is not available."
    };

    var readOnlyMessage = "The configuration is read-only; change it through the configured external provider.";

    var success = function (message, data) {
        return { ok: true, message: message, data: data };
    };

    var failure = function (response) {
        if (response.status === 403 && response.data && response.data.code === readOnlyCode) {
            return { ok: false, message: readOnlyMessage };
        }

        return { ok: false, message: failures[response.status] || genericFailure };
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
                    ? success("The settings were saved. Some of them apply after the application restarts.")
                    : failure(response);
            });
        },

        generate: function (file, fieldPath) {
            return action(file, "generate", fieldPath, "A new value was generated. It applies after the application restarts.");
        },

        rotate: function (file, fieldPath) {
            return action(file, "rotate", fieldPath, "The secret was rotated.");
        },

        loadEmail: function () {
            return http.get("/settings/email").then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        saveEmail: function (request) {
            return http.put("/settings/email", request).then(function (response) {
                return response.ok
                    ? success("The email settings were saved. They apply after the application restarts.")
                    : failure(response);
            });
        },

        loadLogging: function (file) {
            return http.get(filePath(file, "logging")).then(function (response) {
                return response.ok ? success("", response.data) : failure(response);
            });
        },

        saveLogging: function (file, request) {
            return http.put(filePath(file, "logging"), request).then(function (response) {
                return response.ok
                    ? success("The log levels were saved. They apply after the application restarts.")
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
