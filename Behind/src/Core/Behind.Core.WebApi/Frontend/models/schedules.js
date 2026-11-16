define(["models/http", "models/results", "models/paging"], function (http, results, pagination) {
    var outcome = results.create({
        "invalid": "schedules.conflict.invalid",
        "invalid-cron": "schedules.conflict.invalidCron",
        "unknown-action": "schedules.conflict.unknownAction",
        "invalid-participants": "schedules.conflict.invalidParticipants",
        "version-conflict": "schedules.conflict.versionConflict",
        "already-running": "schedules.conflict.alreadyRunning",
        "session-cleanup-invalid-age": "schedules.conflict.sessionCleanupInvalidAge"
    });
    var loaded = outcome.loaded;
    var saved = function (response, message, failures) {
        return outcome.saved(response, message, failures);
    };
    var readFailures = { 401: "common.error.sessionExpired", 403: "schedules.error.readDenied" };
    var writeFailures = { 400: "schedules.error.invalid", 401: "common.error.sessionExpired", 403: "schedules.error.writeDenied", 404: "schedules.error.notFound", 409: "schedules.error.saveFailed" };
    var deleteFailures = { 401: "common.error.sessionExpired", 403: "schedules.error.deleteDenied", 404: "schedules.error.notFound", 409: "schedules.error.changeFailed" };
    var manageFailures = { 401: "common.error.sessionExpired", 403: "schedules.error.manageDenied", 404: "schedules.error.notFound", 409: "schedules.error.runAlreadyRunning" };
    var query = function (values) {
        return Object.keys(values).filter(function (key) {
            return values[key] !== undefined && values[key] !== null && values[key] !== "" && values[key] !== false;
        }).map(function (key) {
            return encodeURIComponent(key) + "=" + encodeURIComponent(values[key]);
        }).join("&");
    };
    var mutate = function (url, message, failures) {
        return http.post(url).then(function (response) {
            return saved(response, message, failures);
        });
    };

    return {
        get: function (id) {
            return http.get("/schedules/" + encodeURIComponent(id)).then(function (response) { return loaded(response, readFailures); });
        },
        manage: function (filters) {
            return http.get(pagination.managing("/schedules/manage", filters)).then(function (response) { return loaded(response, filters.deleted ? { 401: readFailures[401], 403: "schedules.error.deletedReadDenied" } : readFailures); });
        },
        actions: function () {
            return http.get("/schedules/actions").then(function (response) { return loaded(response, readFailures); });
        },
        actionOptions: function (action, parameter) {
            return http.get("/schedules/actions/" + encodeURIComponent(action) + "/options/" + encodeURIComponent(parameter)).then(function (response) { return loaded(response, writeFailures); });
        },
        create: function (fields) {
            return http.post("/schedules", fields).then(function (response) { return saved(response, "schedules.done.saved", writeFailures); });
        },
        save: function (id, fields) {
            return http.post("/schedules/" + encodeURIComponent(id), fields).then(function (response) { return saved(response, "schedules.done.saved", writeFailures); });
        },
        remove: function (id) { return mutate("/schedules/" + encodeURIComponent(id) + "/delete", "schedules.done.deleted", deleteFailures); },
        restore: function (id) { return mutate("/schedules/" + encodeURIComponent(id) + "/restore", "schedules.done.restored", deleteFailures); },
        hardDelete: function (id) { return mutate("/schedules/" + encodeURIComponent(id) + "/hard/delete", "schedules.done.permanentlyDeleted", deleteFailures); },
        run: function (id) {
            return http.post("/schedules/" + encodeURIComponent(id) + "/run").then(function (response) { return saved(response, "schedules.done.runQueued", manageFailures); });
        },
        cron: function (expression, count) {
            return http.get("/schedules/cron?" + query({ expression: expression, count: count })).then(function (response) { return loaded(response, readFailures); });
        },
        participants: function (search) {
            return http.get("/schedules/participants?" + query({ search: search })).then(function (response) { return loaded(response, writeFailures); });
        },
        runs: function (filters) {
            return http.get("/schedules/runs?" + query(filters)).then(function (response) { return loaded(response, filters.deleted ? { 401: readFailures[401], 403: "schedules.error.deletedReadDenied" } : readFailures); });
        },
        removeRun: function (id) { return mutate("/schedules/runs/" + encodeURIComponent(id) + "/delete", "schedules.done.deleted", deleteFailures); },
        restoreRun: function (id) { return mutate("/schedules/runs/" + encodeURIComponent(id) + "/restore", "schedules.done.restored", deleteFailures); },
        hardDeleteRun: function (id) { return mutate("/schedules/runs/" + encodeURIComponent(id) + "/hard/delete", "schedules.done.permanentlyDeleted", deleteFailures); }
    };
});
