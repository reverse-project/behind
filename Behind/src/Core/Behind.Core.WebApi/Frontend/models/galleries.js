define(["models/http", "models/results", "models/paging"], function (http, results, pagination) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "unknown-image": "One of the images could not be found. Upload it again.",
        "version-conflict": "The gallery was changed at the same time by someone else. Reload it and try again."
    });

    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var publicFailures = {
        404: "This gallery does not exist."
    };

    var readFailures = {
        401: sessionExpired,
        403: "You need gallery access to see galleries.",
        404: "This gallery no longer exists."
    };

    var writeFailures = {
        400: "Enter a title for the gallery.",
        401: sessionExpired,
        403: "You are not allowed to change galleries.",
        404: "This gallery no longer exists.",
        409: "The gallery could not be saved."
    };

    var removeFailures = {
        401: sessionExpired,
        403: "You are not allowed to delete galleries.",
        404: "This gallery no longer exists."
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "You need developer access to see deleted galleries."
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "You need developer access to restore or permanently delete galleries.",
        404: "This gallery is not deleted.",
        409: "The gallery could not be changed."
    };

    var uploadFailures = {
        400: "Choose the images to upload.",
        401: sessionExpired,
        403: "You are not allowed to upload images.",
        413: "The images are too large. Upload fewer or smaller images at a time."
    };

    var thumbnailFailures = {
        400: "Choose an image to upload.",
        401: sessionExpired,
        403: "You are not allowed to upload images.",
        413: "The image is too large. Choose a smaller one.",
        415: "Only PNG, JPEG, GIF and WebP images can be uploaded."
    };

    var fileErrors = {
        unsupported: "Only PNG, JPEG, GIF and WebP images can be uploaded.",
        "too-large": "The image is too large.",
        unreadable: "The image could not be read."
    };

    var draft = function (fields) {
        return {
            title: fields.title,
            description: fields.description,
            thumbnailImageId: fields.thumbnailImageId || null,
            items: (fields.items || []).map(function (item) {
                return {
                    imageId: item.imageId,
                    thumbnailId: item.thumbnailId,
                    description: item.description
                };
            }),
            publish: !!fields.publish
        };
    };

    var paging = pagination.paging;

    var managing = pagination.managing;

    var listing = function (query) {
        return "/galleries?" + paging(query)
            + (query.search ? "&search=" + encodeURIComponent(query.search) : "");
    };

    var uploaded = function (image) {
        return image.error
            ? { ok: false, fileName: image.fileName, message: fileErrors[image.error] || "The image could not be uploaded." }
            : { ok: true, fileName: image.fileName, image: image };
    };

    return {
        uploadBatchSize: 5,

        list: function (query) {
            return http.get(listing(query)).then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        manage: function (query) {
            return http.get(managing("/galleries/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        history: function (id) {
            return http.get("/galleries/" + id + "/versions").then(function (response) {
                return loaded(response, readFailures);
            });
        },

        version: function (id, version) {
            return http.get("/galleries/" + id + "/versions/" + version).then(function (response) {
                return loaded(response, readFailures);
            });
        },

        get: function (id) {
            return http.get("/galleries/" + id).then(function (response) {
                return loaded(response, publicFailures);
            });
        },

        create: function (fields) {
            return http.post("/galleries", draft(fields)).then(function (response) {
                return saved(response, fields.publish ? "The gallery was published." : "The draft was saved.", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/galleries/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, fields.publish ? "The gallery was published." : "A new version was saved.", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/galleries/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "The version is now live.", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/galleries/" + id + "/delete").then(function (response) {
                return saved(response, "The gallery was deleted.", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/galleries/" + id + "/restore").then(function (response) {
                return saved(response, "The gallery was restored.", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/galleries/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The gallery was permanently deleted.", deletedActionFailures);
            });
        },

        uploadImages: function (files) {
            var form = new FormData();

            Array.prototype.forEach.call(files, function (file) {
                form.append("files", file);
            });

            return http.postForm("/galleries/images", form).then(function (response) {
                return response.ok
                    ? { ok: true, message: "", images: response.data.images.map(uploaded) }
                    : failure(response, uploadFailures);
            });
        },

        uploadThumbnail: function (file) {
            var form = new FormData();

            form.append("file", file);

            return http.postForm("/galleries/thumbnail", form).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, thumbnailFailures);
            });
        }
    };
});
