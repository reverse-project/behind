define(["models/http", "models/results", "modules/sport/models/components"], function (http, results) {
    var sessionExpired = results.sessionExpired;
    var root = "/sport/";

    var outcome = results.create({
        "version-conflict": "sport.conflict.versionConflict",
        "unknown-reference": "sport.conflict.unknownReference",
        "mismatch": "sport.conflict.mismatch",
        "duplicate": "sport.conflict.duplicate",
        "unknown-image": "sport.conflict.unknownImage",
        "invalid-range": "sport.conflict.invalidRange"
    });

    var failure = outcome.failure;
    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var listFailures = {};

    var detailFailures = {
        404: "sport.error.notFound"
    };

    var readFailures = {
        401: sessionExpired,
        403: "sport.error.readDenied"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "sport.error.deletedReadDenied"
    };

    var writeFailures = {
        400: "sport.error.invalid",
        401: sessionExpired,
        403: "sport.error.writeDenied",
        404: "sport.error.notFound",
        409: "sport.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "sport.error.deleteDenied",
        404: "sport.error.notFound"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "sport.error.deletedActionDenied",
        404: "sport.error.notDeleted",
        409: "sport.error.changeFailed"
    };

    var imageFailures = {
        400: "sport.error.imageRequired",
        401: sessionExpired,
        403: "sport.error.imageDenied",
        413: "sport.error.imageTooLarge",
        415: "sport.error.imageType"
    };

    var queryString = function (query) {
        return Object.keys(query)
            .filter(function (name) {
                var value = query[name];

                return value !== null && value !== undefined && value !== "" && value !== false;
            })
            .map(function (name) {
                return name + "=" + encodeURIComponent(query[name]);
            })
            .join("&");
    };

    var listUrl = function (resource, query, suffix) {
        return root + resource + suffix + "?" + queryString(query);
    };

    var read = function (path, failures) {
        return http.get(path).then(function (response) {
            return loaded(response, failures);
        });
    };

    var post = function (path, body, done, failures) {
        return http.post(path, body).then(function (response) {
            return saved(response, done, failures);
        });
    };

    return {
        list: function (resource, query) {
            return read(listUrl(resource, query, ""), listFailures);
        },

        manage: function (resource, query) {
            return read(listUrl(resource, query, "/manage"), query.deleted ? deletedReadFailures : readFailures);
        },

        create: function (resource, draft) {
            return post(root + resource, draft, "sport.done.saved", writeFailures);
        },

        save: function (resource, id, draft) {
            return post(root + resource + "/" + id, draft, "sport.done.saved", writeFailures);
        },

        remove: function (resource, id) {
            return post(root + resource + "/" + id + "/delete", undefined, "sport.done.deleted", removeFailures);
        },

        restore: function (resource, id) {
            return post(root + resource + "/" + id + "/restore", undefined, "sport.done.restored", deletedActionFailures);
        },

        hardDelete: function (resource, id) {
            return post(
                root + resource + "/" + id + "/hard/delete",
                undefined,
                "sport.done.permanentlyDeleted",
                deletedActionFailures
            );
        },

        uploadImage: function (resource, file) {
            var form = new FormData();

            form.append("file", file);

            return http.postForm(root + resource + "/images", form).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, imageFailures);
            });
        },

        discipline: function (id) {
            return read(root + "disciplines/" + id, detailFailures);
        },

        team: function (id) {
            return read(root + "teams/" + id, detailFailures);
        },

        player: function (id) {
            return read(root + "players/" + id, detailFailures);
        },

        season: function (id) {
            return read(root + "seasons/" + id, detailFailures);
        },

        leaderboards: function (seasonId, limit) {
            return read(root + "leaderboards/" + seasonId + "?limit=" + limit, detailFailures);
        },

        leaderboard: function (seasonId, statTypeId, limit) {
            return read(root + "leaderboards/" + seasonId + "/" + statTypeId + "?limit=" + limit, detailFailures);
        }
    };
});
