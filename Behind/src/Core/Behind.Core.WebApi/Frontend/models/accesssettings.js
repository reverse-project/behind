define(["models/http", "models/results", "models/paging"], function (http, results, paging) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "unknown-role": "accessSettings.conflict.unknownRole",
        "version-conflict": "accessSettings.conflict.versionConflict"
    });

    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var readFailures = {
        401: sessionExpired,
        403: "accessSettings.error.readDenied"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "accessSettings.error.deletedReadDenied",
        404: "accessSettings.error.noDeleted"
    };

    var writeFailures = {
        400: "accessSettings.error.invalid",
        401: sessionExpired,
        403: "accessSettings.error.writeDenied",
        409: "accessSettings.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "accessSettings.error.deleteDenied",
        404: "accessSettings.error.notFound"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "accessSettings.error.deletedActionDenied",
        404: "accessSettings.error.notDeleted",
        409: "accessSettings.error.changeFailed"
    };

    var approvalFailures = {
        401: sessionExpired,
        403: "accessSettings.error.approvalDenied",
        404: "accessSettings.error.notPending"
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
            restrictionEnabled: fields.restrictionEnabled,
            restrictedToRole: fields.restrictedToRole,
            restrictionTitle: fields.restrictionTitle,
            restrictionMessage: fields.restrictionMessage,
            moderatedSignUp: fields.moderatedSignUp
        };
    };

    var failure = outcome.failure;

    return {
        uploadImage: function (file) {
            var form = new FormData();

            form.append("file", file);

            return http.postForm("/access/images", form).then(function (response) {
                return response.ok ? { ok: true, message: "", data: response.data } : failure(response, imageFailures);
            });
        },

        manage: function (deleted) {
            return http.get("/access/manage" + (deleted ? "?deleted=true" : "")).then(function (response) {
                return loaded(response, deleted ? deletedReadFailures : readFailures);
            });
        },

        save: function (fields) {
            return http.post("/access", draft(fields)).then(function (response) {
                return saved(response, "accessSettings.done.saved", writeFailures);
            });
        },

        remove: function () {
            return http.post("/access/delete").then(function (response) {
                return saved(response, "accessSettings.done.deleted", removeFailures);
            });
        },

        restore: function () {
            return http.post("/access/restore").then(function (response) {
                return saved(response, "accessSettings.done.restored", deletedActionFailures);
            });
        },

        hardDelete: function () {
            return http.post("/access/hard/delete").then(function (response) {
                return saved(response, "accessSettings.done.permanentlyDeleted", deletedActionFailures);
            });
        },

        pendingUsers: function (query) {
            return http.get(paging.managing("/access/pending-users", query)).then(function (response) {
                var result = loaded(response, readFailures);

                return result.ok ? { ok: true, message: "", data: { users: result.data.users, letters: [] } } : result;
            });
        },

        approve: function (userId) {
            return http.post("/access/pending-users/" + userId + "/approve").then(function (response) {
                return saved(response, "accessSettings.done.approved", approvalFailures);
            });
        },

        reject: function (userId) {
            return http.post("/access/pending-users/" + userId + "/reject").then(function (response) {
                return saved(response, "accessSettings.done.rejected", approvalFailures);
            });
        }
    };
});
