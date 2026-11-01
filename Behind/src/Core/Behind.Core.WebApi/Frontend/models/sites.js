define(["models/http"], function (http) {
    var genericFailure = "Something went wrong. Please try again.";

    var sessionExpired = "Your session has expired. Please sign in again.";

    var failure = function (response, messages) {
        return { ok: false, message: messages[response.status] || genericFailure };
    };

    var conflictMessages = {
        "slug-taken": "Another site already uses this slug. Choose a different one.",
        "version-conflict": "The site was changed at the same time by someone else. Reload it and try again."
    };

    var conflict = function (response, messages) {
        var reason = response.data && response.data.reason ? response.data.reason : "";

        return { ok: false, message: conflictMessages[reason] || messages[409] || genericFailure };
    };

    var loaded = function (response, messages) {
        return response.ok ? { ok: true, message: "", data: response.data } : failure(response, messages);
    };

    var saved = function (response, done, messages) {
        if (response.ok) {
            return { ok: true, message: done, data: response.data };
        }

        return response.status === 409 ? conflict(response, messages) : failure(response, messages);
    };

    var readFailures = {
        401: sessionExpired,
        403: "You need site access to see sites.",
        404: "This site no longer exists."
    };

    var writeFailures = {
        400: "Enter a title and a slug made of letters, numbers and dashes.",
        401: sessionExpired,
        403: "You are not allowed to change sites.",
        404: "This site no longer exists.",
        409: "The site could not be saved."
    };

    var imageFailures = {
        400: "Choose an image to upload.",
        401: sessionExpired,
        403: "You are not allowed to upload images.",
        413: "The image is too large. Choose a smaller one.",
        415: "Only PNG, JPEG, GIF and WebP images can be uploaded."
    };

    var publicFailures = {
        404: "This page does not exist."
    };

    var draft = function (fields) {
        return {
            title: fields.title,
            slug: fields.slug,
            content: fields.content,
            publish: !!fields.publish
        };
    };

    return {
        list: function () {
            return http.get("/sites").then(function (response) {
                return loaded(response, readFailures);
            });
        },

        settings: function () {
            return http.get("/sites/settings").then(function (response) {
                return loaded(response, readFailures);
            });
        },

        history: function (id) {
            return http.get("/sites/" + id).then(function (response) {
                return loaded(response, readFailures);
            });
        },

        version: function (id, version) {
            return http.get("/sites/" + id + "/versions/" + version).then(function (response) {
                return loaded(response, readFailures);
            });
        },

        create: function (fields) {
            return http.post("/sites", draft(fields)).then(function (response) {
                return saved(response, "The site was saved.", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/sites/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, "A new version was saved.", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/sites/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "The version is now live.", writeFailures);
            });
        },

        uploadImage: function (file) {
            var form = new FormData();

            form.append("file", file);

            return http.postForm("/sites/images", form).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, imageFailures);
            });
        },

        loadIndex: function () {
            return http.get("/sites/index").then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        loadPublic: function (slug) {
            return http.get("/sites/slug/" + encodeURIComponent(slug)).then(function (response) {
                return loaded(response, publicFailures);
            });
        }
    };
});
