define(["models/http", "models/results"], function (http, results) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "unknown-category": "The selected category no longer exists. Choose another one.",
        "invalid-schedule": "The end of the news must be after its start.",
        "version-conflict": "The news was changed at the same time by someone else. Reload it and try again.",
        "name-taken": "Another category already uses this name. Choose a different one.",
        "unknown-image": "The header image could not be found. Upload it again."
    });

    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var publicFailures = {
        404: "This news does not exist."
    };

    var readFailures = {
        401: sessionExpired,
        403: "You need news access to see news.",
        404: "This news no longer exists."
    };

    var writeFailures = {
        400: "Enter a title for the news.",
        401: sessionExpired,
        403: "You are not allowed to change news.",
        404: "This news no longer exists.",
        409: "The news could not be saved."
    };

    var removeFailures = {
        401: sessionExpired,
        403: "You are not allowed to delete news.",
        404: "This news no longer exists."
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "You need developer access to see deleted news."
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "You need developer access to restore or permanently delete news.",
        404: "This news is not deleted.",
        409: "The news could not be changed."
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

    var commentFailures = {
        400: "Write a comment of up to 2000 characters.",
        401: "Sign in to comment.",
        404: "This news no longer exists."
    };

    var commentEditFailures = {
        400: "Write a comment of up to 2000 characters.",
        401: "Sign in to edit comments.",
        403: "You are not allowed to edit this comment.",
        404: "This comment no longer exists."
    };

    var commentRemoveFailures = {
        401: "Sign in to delete comments.",
        403: "You are not allowed to delete this comment.",
        404: "This comment no longer exists."
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

    var paging = function (query) {
        return "page=" + query.page + "&pageSize=" + query.pageSize;
    };

    var managing = function (path, query) {
        return path + "?" + paging(query)
            + "&search=" + encodeURIComponent(query.search) + "&letter=" + encodeURIComponent(query.letter)
            + (query.deleted ? "&deleted=true" : "");
    };

    return {
        list: function (query) {
            var url = "/news?" + paging(query) + (query.category ? "&category=" + encodeURIComponent(query.category) : "");

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
                return saved(response, "The news was saved.", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/news/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, "A new version was saved.", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/news/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "The version is now live.", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/news/" + id + "/delete").then(function (response) {
                return saved(response, "The news was deleted.", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/news/" + id + "/restore").then(function (response) {
                return saved(response, "The news was restored.", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/news/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The news was permanently deleted.", deletedActionFailures);
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
                return saved(response, "The category was saved.", categoryWriteFailures);
            });
        },

        saveCategory: function (id, fields) {
            return http.post("/news/categories/" + id, categoryDraft(fields)).then(function (response) {
                return saved(response, "The category was saved.", categoryWriteFailures);
            });
        },

        removeCategory: function (id) {
            return http.post("/news/categories/" + id + "/delete").then(function (response) {
                return saved(response, "The category was deleted.", categoryRemoveFailures);
            });
        },

        restoreCategory: function (id) {
            return http.post("/news/categories/" + id + "/restore").then(function (response) {
                return saved(response, "The category was restored.", categoryDeletedActionFailures);
            });
        },

        hardDeleteCategory: function (id) {
            return http.post("/news/categories/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The category was permanently deleted.", categoryDeletedActionFailures);
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
                return saved(response, "Your comment was added.", commentFailures);
            });
        },

        editComment: function (commentId, content) {
            return http.post("/news/comments/" + commentId, { content: content }).then(function (response) {
                return saved(response, "The comment was changed.", commentEditFailures);
            });
        },

        removeComment: function (commentId) {
            return http.post("/news/comments/" + commentId + "/delete").then(function (response) {
                return saved(response, "The comment was deleted.", commentRemoveFailures);
            });
        }
    };
});
