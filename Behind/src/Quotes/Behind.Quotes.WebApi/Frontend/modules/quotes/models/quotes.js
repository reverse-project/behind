define(["models/http", "models/results", "models/paging"], function (http, results, pagination) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "version-conflict": "quotes.conflict.versionConflict"
    });

    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var listFailures = {};

    var readFailures = {
        401: sessionExpired,
        403: "quotes.error.readDenied"
    };

    var writeFailures = {
        400: "quotes.error.invalid",
        401: sessionExpired,
        403: "quotes.error.writeDenied",
        404: "quotes.error.notFound",
        409: "quotes.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "quotes.error.deleteDenied",
        404: "quotes.error.notFound"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "quotes.error.deletedReadDenied"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "quotes.error.deletedActionDenied",
        404: "quotes.error.notDeleted",
        409: "quotes.error.changeFailed"
    };

    var draft = function (fields) {
        return {
            text: fields.text,
            author: fields.author
        };
    };

    return {
        list: function (query) {
            return http.get("/quotes?" + pagination.paging(query)).then(function (response) {
                return loaded(response, listFailures);
            });
        },

        manage: function (query) {
            return http.get(pagination.managing("/quotes/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        create: function (fields) {
            return http.post("/quotes", draft(fields)).then(function (response) {
                return saved(response, "quotes.done.saved", writeFailures);
            });
        },

        save: function (id, fields) {
            return http.post("/quotes/" + id, draft(fields)).then(function (response) {
                return saved(response, "quotes.done.saved", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/quotes/" + id + "/delete").then(function (response) {
                return saved(response, "quotes.done.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/quotes/" + id + "/restore").then(function (response) {
                return saved(response, "quotes.done.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/quotes/" + id + "/hard/delete").then(function (response) {
                return saved(response, "quotes.done.permanentlyDeleted", deletedActionFailures);
            });
        }
    };
});
