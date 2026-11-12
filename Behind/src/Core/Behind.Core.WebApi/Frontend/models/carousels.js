define(["models/http", "models/results"], function (http, results) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({ "version-conflict": "carousels.conflict.versionConflict" });
    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var readFailures = {
        401: sessionExpired,
        403: "carousels.error.readDenied"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "carousels.error.deletedReadDenied"
    };

    var writeFailures = {
        400: "carousels.error.invalid",
        401: sessionExpired,
        403: "carousels.error.writeDenied",
        404: "carousels.error.siteMissing",
        409: "carousels.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "carousels.error.deleteDenied",
        404: "carousels.error.notFound"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "carousels.error.deletedActionDenied",
        404: "carousels.error.notDeleted",
        409: "carousels.error.changeFailed"
    };

    var imageFailures = {
        400: "carousels.error.imageRequired",
        401: sessionExpired,
        403: "carousels.error.imageDenied",
        413: "carousels.error.imageTooLarge",
        415: "carousels.error.imageType"
    };

    var fallbackSettings = { maxSlides: 20, minIntervalSeconds: 2, maxIntervalSeconds: 60, defaultIntervalSeconds: 5, maxCaptionLength: 300, maxLinkUrlLength: 2000, maxImageBytes: 0 };

    var url = function (siteId, action) {
        return "/sites/" + siteId + "/carousel" + (action || "");
    };

    var draft = function (fields) {
        return {
            intervalSeconds: fields.intervalSeconds,
            slides: fields.slides.map(function (slide) {
                return { id: slide.id || null, imageId: slide.imageId, caption: slide.caption, linkUrl: slide.linkUrl };
            })
        };
    };

    return {
        settings: function () {
            return http.get("/carousels/settings").then(function (response) {
                return response.ok ? response.data : fallbackSettings;
            });
        },

        loadPublic: function (siteId) {
            return http.get("/carousels/sites/" + siteId).then(function (response) {
                return response.ok ? response.data : null;
            });
        },

        loadManaged: function (siteId, deleted) {
            return http.get(url(siteId) + (deleted ? "?deleted=true" : "")).then(function (response) {
                if (response.status === 404) {
                    return { ok: true, missing: true, message: "", data: null };
                }

                var result = loaded(response, deleted ? deletedReadFailures : readFailures);

                return { ok: result.ok, missing: false, message: result.message, data: result.data };
            });
        },

        save: function (siteId, fields) {
            return http.post(url(siteId), draft(fields)).then(function (response) {
                return saved(response, "carousels.done.saved", writeFailures);
            });
        },

        remove: function (siteId) {
            return http.post(url(siteId, "/delete")).then(function (response) {
                return saved(response, "carousels.done.deleted", removeFailures);
            });
        },

        restore: function (siteId) {
            return http.post(url(siteId, "/restore")).then(function (response) {
                return saved(response, "carousels.done.restored", deletedActionFailures);
            });
        },

        hardDelete: function (siteId) {
            return http.post(url(siteId, "/hard/delete")).then(function (response) {
                return saved(response, "carousels.done.permanentlyDeleted", deletedActionFailures);
            });
        },

        uploadImage: function (file) {
            var form = new FormData();

            form.append("file", file);

            return http.postForm("/carousels/images", form).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, imageFailures);
            });
        }
    };
});
