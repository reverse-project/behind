define(["models/http", "models/results", "models/paging", "models/invalidation"], function (http, results, pagination, invalidation) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "name-taken": "messages.conflict.nameTaken",
        "version-conflict": "messages.conflict.versionConflict"
    });

    var loaded = outcome.loaded;
    var saved = function (response, message, failures) {
        return invalidation.after("widget:core.message", outcome.saved(response, message, failures));
    };

    var readFailures = {
        401: sessionExpired,
        403: "messages.error.readDenied",
        404: "messages.error.notFound"
    };

    var writeFailures = {
        400: "messages.error.invalid",
        401: sessionExpired,
        403: "messages.error.writeDenied",
        404: "messages.error.notFound",
        409: "messages.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "messages.error.deleteDenied",
        404: "messages.error.notFound"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "messages.error.deletedReadDenied"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "messages.error.deletedActionDenied",
        404: "messages.error.notDeleted",
        409: "messages.error.changeFailed"
    };

    var draft = function (fields) {
        return {
            name: fields.name,
            title: fields.title,
            content: fields.content,
            level: fields.level,
            startsAt: fields.startsAt,
            endsAt: fields.endsAt
        };
    };

    return {
        get: function (id) {
            return http.get("/messages/" + id).then(function (response) {
                return loaded(response, readFailures);
            });
        },

        manage: function (query) {
            return http.get(pagination.managing("/messages/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        create: function (fields) {
            return http.post("/messages", draft(fields)).then(function (response) {
                return saved(response, "messages.done.saved", writeFailures);
            });
        },

        save: function (id, fields) {
            return http.post("/messages/" + id, draft(fields)).then(function (response) {
                return saved(response, "messages.done.saved", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/messages/" + id + "/delete").then(function (response) {
                return saved(response, "messages.done.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/messages/" + id + "/restore").then(function (response) {
                return saved(response, "messages.done.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/messages/" + id + "/hard/delete").then(function (response) {
                return saved(response, "messages.done.permanentlyDeleted", deletedActionFailures);
            });
        }
    };
});
