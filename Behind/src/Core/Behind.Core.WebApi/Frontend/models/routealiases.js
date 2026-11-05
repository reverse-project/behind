define(["models/http", "models/results", "models/paging"], function (http, results, pagination) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "alias-taken": "routeAliases.conflict.aliasTaken",
        "version-conflict": "routeAliases.conflict.versionConflict"
    });

    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var readFailures = {
        401: sessionExpired,
        403: "routeAliases.error.readDenied"
    };

    var writeFailures = {
        400: "routeAliases.error.invalid",
        401: sessionExpired,
        403: "routeAliases.error.writeDenied",
        404: "routeAliases.error.notFound",
        409: "routeAliases.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "routeAliases.error.deleteDenied",
        404: "routeAliases.error.notFound"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "routeAliases.error.deletedReadDenied"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "routeAliases.error.deletedActionDenied",
        404: "routeAliases.error.notDeleted",
        409: "routeAliases.error.changeFailed"
    };

    var draft = function (fields) {
        return {
            alias: fields.alias,
            target: fields.target
        };
    };

    return {
        resolve: function (alias) {
            return http.get("/route-aliases/resolve/" + encodeURIComponent(alias)).then(function (response) {
                return response.ok ? { ok: true, data: response.data } : { ok: false };
            });
        },

        manage: function (query) {
            return http.get(pagination.managing("/route-aliases/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        create: function (fields) {
            return http.post("/route-aliases", draft(fields)).then(function (response) {
                return saved(response, "routeAliases.done.saved", writeFailures);
            });
        },

        save: function (id, fields) {
            return http.post("/route-aliases/" + id, draft(fields)).then(function (response) {
                return saved(response, "routeAliases.done.saved", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/route-aliases/" + id + "/delete").then(function (response) {
                return saved(response, "routeAliases.done.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/route-aliases/" + id + "/restore").then(function (response) {
                return saved(response, "routeAliases.done.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/route-aliases/" + id + "/hard/delete").then(function (response) {
                return saved(response, "routeAliases.done.permanentlyDeleted", deletedActionFailures);
            });
        }
    };
});
