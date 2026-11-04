define(["models/http", "models/results", "models/paging"], function (http, results, pagination) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "unknown-category": "content.error.unknownCategory",
        "invalid-schedule": "news.error.invalidSchedule",
        "version-conflict": "news.error.versionConflict",
        "name-taken": "content.error.nameTaken",
        "unknown-image": "content.error.unknownImage"
    });

    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var publicFailures = {
        404: "news.error.doesNotExist"
    };

    var readFailures = {
        401: sessionExpired,
        403: "news.error.readForbidden",
        404: "news.error.gone"
    };

    var writeFailures = {
        400: "news.error.titleRequired",
        401: sessionExpired,
        403: "news.error.writeForbidden",
        404: "news.error.gone",
        409: "news.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "news.error.deleteForbidden",
        404: "news.error.gone"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "news.error.developerRead"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "news.error.developerAction",
        404: "news.error.notDeleted",
        409: "news.error.changeFailed"
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

    var commentFailures = {
        400: "news.error.commentInvalid",
        401: "news.error.commentSignIn",
        404: "news.error.gone"
    };

    var commentEditFailures = {
        400: "news.error.commentInvalid",
        401: "news.error.commentEditSignIn",
        403: "news.error.commentEditForbidden",
        404: "news.error.commentGone"
    };

    var commentRemoveFailures = {
        401: "news.error.commentDeleteSignIn",
        403: "news.error.commentDeleteForbidden",
        404: "news.error.commentGone"
    };

    var draft = function (fields) {
        return {
            title: fields.title,
            summary: fields.summary,
            content: fields.content,
            categoryId: fields.categoryId || null,
            highlighted: !!fields.highlighted,
            publish: !!fields.publish,
            startAt: fields.startAt || null,
            endAt: fields.endAt || null
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

    return {
        list: function (query) {
            var url = "/news?" + paging(query)
                + (query.category ? "&category=" + encodeURIComponent(query.category) : "")
                + (query.letter ? "&letter=" + encodeURIComponent(query.letter) : "");

            return http.get(url).then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        manage: function (query) {
            return http.get(managing("/news/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        history: function (id) {
            return http.get("/news/" + id + "/versions").then(function (response) {
                return loaded(response, readFailures);
            });
        },

        version: function (id, version) {
            return http.get("/news/" + id + "/versions/" + version).then(function (response) {
                return loaded(response, readFailures);
            });
        },

        get: function (id) {
            return http.get("/news/" + id).then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        create: function (fields) {
            return http.post("/news", draft(fields)).then(function (response) {
                return saved(response, "news.saved", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/news/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, "content.versionSaved", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/news/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "content.versionLive", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/news/" + id + "/delete").then(function (response) {
                return saved(response, "news.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/news/" + id + "/restore").then(function (response) {
                return saved(response, "news.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/news/" + id + "/hard/delete").then(function (response) {
                return saved(response, "news.permanentlyDeleted", deletedActionFailures);
            });
        },

        categories: function () {
            return http.get("/news/categories").then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        manageCategories: function (query) {
            return http.get(managing("/news/categories/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? categoryDeletedReadFailures : readFailures);
            });
        },

        createCategory: function (fields) {
            return http.post("/news/categories", categoryDraft(fields)).then(function (response) {
                return saved(response, "content.categorySaved", categoryWriteFailures);
            });
        },

        saveCategory: function (id, fields) {
            return http.post("/news/categories/" + id, categoryDraft(fields)).then(function (response) {
                return saved(response, "content.categorySaved", categoryWriteFailures);
            });
        },

        removeCategory: function (id) {
            return http.post("/news/categories/" + id + "/delete").then(function (response) {
                return saved(response, "content.categoryDeleted", categoryRemoveFailures);
            });
        },

        restoreCategory: function (id) {
            return http.post("/news/categories/" + id + "/restore").then(function (response) {
                return saved(response, "content.categoryRestored", categoryDeletedActionFailures);
            });
        },

        hardDeleteCategory: function (id) {
            return http.post("/news/categories/" + id + "/hard/delete").then(function (response) {
                return saved(response, "content.categoryPermanentlyDeleted", categoryDeletedActionFailures);
            });
        },

        uploadImage: function (file) {
            var form = new FormData();

            form.append("file", file);

            return http.postForm("/news/images", form).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, imageFailures);
            });
        },

        comments: function (id, query) {
            return http.get("/news/" + id + "/comments?" + paging(query)).then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        addComment: function (id, content) {
            return http.post("/news/" + id + "/comments", { content: content }).then(function (response) {
                return saved(response, "news.commentAdded", commentFailures);
            });
        },

        editComment: function (commentId, content) {
            return http.post("/news/comments/" + commentId, { content: content }).then(function (response) {
                return saved(response, "news.commentChanged", commentEditFailures);
            });
        },

        removeComment: function (commentId) {
            return http.post("/news/comments/" + commentId + "/delete").then(function (response) {
                return saved(response, "news.commentDeleted", commentRemoveFailures);
            });
        }
    };
});
