define(["models/http", "models/results", "models/paging"], function (http, results, pagination) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "unknown-category": "The selected category no longer exists. Choose another one.",
        "version-conflict": "The article was changed at the same time by someone else. Reload it and try again.",
        "name-taken": "Another category already uses this name. Choose a different one.",
        "unknown-image": "The header image could not be found. Upload it again."
    });

    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var publicFailures = {
        404: "This article does not exist."
    };

    var readFailures = {
        401: sessionExpired,
        403: "You need article access to see articles.",
        404: "This article no longer exists."
    };

    var writeFailures = {
        400: "Enter a title for the article.",
        401: sessionExpired,
        403: "You are not allowed to change articles.",
        404: "This article no longer exists.",
        409: "The article could not be saved."
    };

    var removeFailures = {
        401: sessionExpired,
        403: "You are not allowed to delete articles.",
        404: "This article no longer exists."
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "You need developer access to see deleted articles."
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "You need developer access to restore or permanently delete articles.",
        404: "This article is not deleted.",
        409: "The article could not be changed."
    };

    var categoryWriteFailures = {
        400: "Enter a name for the category.",
        401: sessionExpired,
        403: "You are not allowed to change categories.",
        404: "This category no longer exists.",
        409: "The category could not be saved."
    };

    var categoryRemoveFailures = {
        401: sessionExpired,
        403: "You are not allowed to delete categories.",
        404: "This category no longer exists."
    };

    var categoryDeletedActionFailures = {
        401: sessionExpired,
        403: "You need developer access to restore or permanently delete categories.",
        404: "This category is not deleted.",
        409: "The category could not be changed. A deleted category cannot come back while another uses its name."
    };

    var categoryDeletedReadFailures = {
        401: sessionExpired,
        403: "You need developer access to see deleted categories."
    };

    var imageFailures = {
        400: "Choose an image to upload.",
        401: sessionExpired,
        403: "You are not allowed to upload images.",
        413: "The image is too large. Choose a smaller one.",
        415: "Only PNG, JPEG, GIF and WebP images can be uploaded."
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
                return saved(response, "The article was saved.", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/articles/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, "A new version was saved.", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/articles/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "The version is now live.", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/articles/" + id + "/delete").then(function (response) {
                return saved(response, "The article was deleted.", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/articles/" + id + "/restore").then(function (response) {
                return saved(response, "The article was restored.", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/articles/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The article was permanently deleted.", deletedActionFailures);
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
                return saved(response, "The category was saved.", categoryWriteFailures);
            });
        },

        saveCategory: function (id, fields) {
            return http.post("/articles/categories/" + id, categoryDraft(fields)).then(function (response) {
                return saved(response, "The category was saved.", categoryWriteFailures);
            });
        },

        removeCategory: function (id) {
            return http.post("/articles/categories/" + id + "/delete").then(function (response) {
                return saved(response, "The category was deleted.", categoryRemoveFailures);
            });
        },

        restoreCategory: function (id) {
            return http.post("/articles/categories/" + id + "/restore").then(function (response) {
                return saved(response, "The category was restored.", categoryDeletedActionFailures);
            });
        },

        hardDeleteCategory: function (id) {
            return http.post("/articles/categories/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The category was permanently deleted.", categoryDeletedActionFailures);
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
