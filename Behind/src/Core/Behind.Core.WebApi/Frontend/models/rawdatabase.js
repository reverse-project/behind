define(["models/http", "models/results", "models/i18n"], function (http, results, i18n) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "read-only": "rawDatabase.conflict.readOnly"
    });

    var readFailures = {
        401: sessionExpired,
        403: "rawDatabase.error.denied",
        404: "rawDatabase.error.tableNotFound"
    };

    var writeFailures = {
        401: sessionExpired,
        403: "rawDatabase.error.denied",
        404: "rawDatabase.error.rowNotFound"
    };

    var rowsUrl = function (table) {
        return "/raw-database/tables/" + encodeURIComponent(table) + "/rows";
    };

    var rowsQuery = function (query) {
        return "?page=" + query.page + "&pageSize=" + query.pageSize
            + "&search=" + encodeURIComponent(query.search)
            + "&orderBy=" + encodeURIComponent(query.orderBy)
            + (query.descending ? "&descending=true" : "");
    };

    var written = function (response, done) {
        if (response.status === 400 && response.data && response.data.errors) {
            return { ok: false, message: i18n.t("rawDatabase.error.invalid", [response.data.errors.join(" ")]) };
        }

        if (response.status === 409 && response.data && response.data.reason === "rejected") {
            return { ok: false, message: i18n.t("rawDatabase.error.rejected", [response.data.message]) };
        }

        return outcome.saved(response, done, writeFailures);
    };

    return {
        tables: function () {
            return http.get("/raw-database/tables").then(function (response) {
                return outcome.loaded(response, readFailures);
            });
        },

        rows: function (table, query) {
            return http.get(rowsUrl(table) + rowsQuery(query)).then(function (response) {
                return outcome.loaded(response, readFailures);
            });
        },

        insert: function (table, values) {
            return http.post(rowsUrl(table), { values: values }).then(function (response) {
                return written(response, "rawDatabase.done.saved");
            });
        },

        update: function (table, key, values) {
            return http.post(rowsUrl(table) + "/update", { key: key, values: values }).then(function (response) {
                return written(response, "rawDatabase.done.saved");
            });
        },

        remove: function (table, key) {
            return http.post(rowsUrl(table) + "/delete", { key: key }).then(function (response) {
                return written(response, "rawDatabase.done.deleted");
            });
        }
    };
});
