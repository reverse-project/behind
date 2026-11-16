define(["models/http", "models/results", "models/paging", "models/invalidation"], function (http, results, pagination, invalidation) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "unknown-category": "content.error.unknownCategory",
        "version-conflict": "articles.error.versionConflict",
        "name-taken": "content.error.nameTaken",
        "unknown-image": "content.error.unknownImage"
    });

    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = function (response, message, failures) {
        return invalidation.after("widget:core.articles", outcome.saved(response, message, failures));
    };

    var publicFailures = {
        404: "articles.error.doesNotExist"
    };

    var readFailures = {
        401: sessionExpired,
        403: "articles.error.readForbidden",
        404: "articles.error.gone"
    };

    var writeFailures = {
        400: "articles.error.titleRequired",
        401: sessionExpired,
        403: "articles.error.writeForbidden",
        404: "articles.error.gone",
        409: "articles.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "articles.error.deleteForbidden",
        404: "articles.error.gone"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "articles.error.developerRead"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "articles.error.developerAction",
        404: "articles.error.notDeleted",
        409: "articles.error.changeFailed"
    };

    var categoryWriteFailures = {
        400: "content.error.categoryNameRequired",
        401: sessionExpired,
        403: "content.error.categoryWriteForbidden",
        404: "content.error.categoryGone",
        409: "content.error.categorySaveFailed"
    };

    var categoryRemoveFailures = {
        401: sessionExpired,
        403: "content.error.categoryDeleteForbidden",
        404: "content.error.categoryGone"
    };

    var categoryDeletedActionFailures = {
        401: sessionExpired,
        403: "content.error.categoryDeveloperAction",
        404: "content.error.categoryNotDeleted",
        409: "content.error.categoryChangeFailed"
    };

    var categoryDeletedReadFailures = {
        401: sessionExpired,
        403: "content.error.categoryDeveloperRead"
    };

    var imageFailures = {
        400: "content.error.imageRequired",
        401: sessionExpired,
        403: "content.error.imageForbidden",
        413: "content.error.imageTooLarge",
        415: "content.error.imageType"
    };

    var draft = function (fields) {
        return {
            title: fields.title,
            summary: fields.summary,
            content: fields.content,
            categoryId: fields.categoryId || null,
            publish: !!fields.publish
        };
    };

    var categoryDraft = function (fields) {
        return {
            name: fields.name,
            headerImageId: fields.headerImageId || null
        };
    };

    var paging = pagination.paging;

    var managing = pagination.managing;

    var listing = function (query) {
        return "/articles?" + paging(query)
            + (query.category ? "&category=" + encodeURIComponent(query.category) : "")
            + (query.search ? "&search=" + encodeURIComponent(query.search) : "");
    };

    return {
        list: function (query) {
            return http.get(listing(query)).then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        manage: function (query) {
            return http.get(managing("/articles/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        history: function (id) {
            return http.get("/articles/" + id + "/versions").then(function (response) {
                return loaded(response, readFailures);
            });
        },

        version: function (id, version) {
            return http.get("/articles/" + id + "/versions/" + version).then(function (response) {
                return loaded(response, readFailures);
            });
        },

        get: function (id) {
            return http.get("/articles/" + id).then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        create: function (fields) {
            return http.post("/articles", draft(fields)).then(function (response) {
                return saved(response, "articles.saved", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/articles/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, "content.versionSaved", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/articles/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "content.versionLive", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/articles/" + id + "/delete").then(function (response) {
                return saved(response, "articles.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/articles/" + id + "/restore").then(function (response) {
                return saved(response, "articles.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/articles/" + id + "/hard/delete").then(function (response) {
                return saved(response, "articles.permanentlyDeleted", deletedActionFailures);
            });
        },

        categories: function () {
            return http.get("/articles/categories").then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        manageCategories: function (query) {
            return http.get(managing("/articles/categories/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? categoryDeletedReadFailures : readFailures);
            });
        },

        createCategory: function (fields) {
            return http.post("/articles/categories", categoryDraft(fields)).then(function (response) {
                return saved(response, "content.categorySaved", categoryWriteFailures);
            });
        },

        saveCategory: function (id, fields) {
            return http.post("/articles/categories/" + id, categoryDraft(fields)).then(function (response) {
                return saved(response, "content.categorySaved", categoryWriteFailures);
            });
        },

        removeCategory: function (id) {
            return http.post("/articles/categories/" + id + "/delete").then(function (response) {
                return saved(response, "content.categoryDeleted", categoryRemoveFailures);
            });
        },

        restoreCategory: function (id) {
            return http.post("/articles/categories/" + id + "/restore").then(function (response) {
                return saved(response, "content.categoryRestored", categoryDeletedActionFailures);
            });
        },

        hardDeleteCategory: function (id) {
            return http.post("/articles/categories/" + id + "/hard/delete").then(function (response) {
                return saved(response, "content.categoryPermanentlyDeleted", categoryDeletedActionFailures);
            });
        },

        uploadImage: function (file) {
            var form = new FormData();

            form.append("file", file);

            return http.postForm("/articles/images", form).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, imageFailures);
            });
        }
    };
});
