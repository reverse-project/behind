define(["models/http", "models/results"], function (http, results) {
    var sessionExpired = results.sessionExpired;

    var conflictMessages = {
        "slug-taken": "Another site already uses this slug. Choose a different one.",
        "version-conflict": "The site was changed at the same time by someone else. Reload it and try again."
    };

    var outcome = results.create(conflictMessages);
    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

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

    var deletedReadFailures = {
        401: sessionExpired,
        403: "You need developer access to see deleted sites."
    };

    var removeFailures = {
        401: sessionExpired,
        403: "You are not allowed to delete sites.",
        404: "This site no longer exists."
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "You need developer access to restore or permanently delete sites.",
        404: "This site is not deleted.",
        409: "The site could not be changed."
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
        list: function (query) {
            var url = "/sites?page=" + query.page + "&pageSize=" + query.pageSize
                + "&search=" + encodeURIComponent(query.search) + "&letter=" + encodeURIComponent(query.letter)
                + (query.deleted ? "&deleted=true" : "");

            return http.get(url).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        remove: function (id) {
            return http.post("/sites/" + id + "/delete").then(function (response) {
                return saved(response, "The site was deleted.", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/sites/" + id + "/restore").then(function (response) {
                return saved(response, "The site was restored.", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/sites/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The site was permanently deleted.", deletedActionFailures);
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
