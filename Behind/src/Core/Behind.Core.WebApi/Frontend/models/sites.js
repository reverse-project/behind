define(["models/http", "models/results"], function (http, results) {
    var sessionExpired = results.sessionExpired;

    var conflictMessages = {
        "slug-taken": "sites.conflict.slugTaken",
        "version-conflict": "sites.conflict.versionConflict"
    };

    var outcome = results.create(conflictMessages);
    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var readFailures = {
        401: sessionExpired,
        403: "sites.error.readDenied",
        404: "sites.error.notFound"
    };

    var writeFailures = {
        400: "sites.error.invalid",
        401: sessionExpired,
        403: "sites.error.writeDenied",
        404: "sites.error.notFound",
        409: "sites.error.saveFailed"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "sites.error.deletedReadDenied"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "sites.error.deleteDenied",
        404: "sites.error.notFound"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "sites.error.deletedActionDenied",
        404: "sites.error.notDeleted",
        409: "sites.error.changeFailed"
    };

    var imageFailures = {
        400: "sites.error.imageRequired",
        401: sessionExpired,
        403: "sites.error.imageDenied",
        413: "sites.error.imageTooLarge",
        415: "sites.error.imageType"
    };

    var publicFailures = {
        404: "sites.error.pageMissing"
    };

    var draft = function (fields) {
        return {
            title: fields.title,
            slug: fields.slug,
            content: fields.content,
            publish: !!fields.publish,
            hideTitle: !!fields.hideTitle,
            sections: fields.sections || []
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
                return saved(response, "sites.done.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/sites/" + id + "/restore").then(function (response) {
                return saved(response, "sites.done.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/sites/" + id + "/hard/delete").then(function (response) {
                return saved(response, "sites.done.permanentlyDeleted", deletedActionFailures);
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
                return saved(response, "sites.done.saved", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/sites/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, "sites.done.versionSaved", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/sites/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "sites.done.live", writeFailures);
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
