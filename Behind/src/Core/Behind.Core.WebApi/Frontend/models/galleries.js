define(["ko", "models/http", "models/results", "models/paging", "models/i18n"], function (ko, http, results, pagination, i18n) {
    var sessionExpired = results.sessionExpired;

    var imageCount = function (count) {
        return count === 1 ? i18n.t("galleries.imageCount.one") : i18n.t("galleries.imageCount.other", [count]);
    };

    var outcome = results.create({
        "unknown-image": "galleries.error.unknownImage",
        "version-conflict": "galleries.error.versionConflict"
    });

    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var publicFailures = {
        404: "galleries.error.notFound"
    };

    var readFailures = {
        401: sessionExpired,
        403: "galleries.error.readDenied",
        404: "galleries.error.gone"
    };

    var writeFailures = {
        400: "galleries.error.titleRequired",
        401: sessionExpired,
        403: "galleries.error.writeDenied",
        404: "galleries.error.gone",
        409: "galleries.error.notSaved"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "galleries.error.deleteDenied",
        404: "galleries.error.gone"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "galleries.error.deletedReadDenied"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "galleries.error.restoreDenied",
        404: "galleries.error.notDeleted",
        409: "galleries.error.notChanged"
    };

    var uploadFailures = {
        400: "galleries.error.chooseImages",
        401: sessionExpired,
        403: "galleries.error.uploadDenied",
        413: "galleries.error.imagesTooLarge"
    };

    var thumbnailFailures = {
        400: "galleries.error.chooseImage",
        401: sessionExpired,
        403: "galleries.error.uploadDenied",
        413: "galleries.error.imageTooLarge",
        415: "galleries.error.imageType"
    };

    var fileErrors = {
        unsupported: "galleries.error.imageType",
        "too-large": "galleries.error.tooLarge",
        unreadable: "galleries.error.unreadable"
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
            ? { ok: false, fileName: image.fileName, message: i18n.t(fileErrors[image.error] || "galleries.error.uploadFailed") }
            : { ok: true, fileName: image.fileName, image: image };
    };

    return {
        uploadBatchSize: 5,

        imageCount: imageCount,

        imageCountLabel: function (count) {
            return ko.pureComputed(function () {
                return imageCount(count);
            });
        },

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
                return saved(response, fields.publish ? "galleries.saved.published" : "galleries.saved.draft", writeFailures);
            });
        },

        saveVersion: function (id, fields) {
            return http.post("/galleries/" + id + "/versions", draft(fields)).then(function (response) {
                return saved(response, fields.publish ? "galleries.saved.published" : "galleries.saved.version", writeFailures);
            });
        },

        activate: function (id, version) {
            return http.post("/galleries/" + id + "/versions/" + version + "/activate").then(function (response) {
                return saved(response, "galleries.saved.live", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/galleries/" + id + "/delete").then(function (response) {
                return saved(response, "galleries.saved.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/galleries/" + id + "/restore").then(function (response) {
                return saved(response, "galleries.saved.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/galleries/" + id + "/hard/delete").then(function (response) {
                return saved(response, "galleries.saved.hardDeleted", deletedActionFailures);
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
