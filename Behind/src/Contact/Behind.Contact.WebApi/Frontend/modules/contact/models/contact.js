define(["models/http", "models/i18n", "models/paging"], function (http, i18n, paging) {
    var result = function (response, failures) {
        return response.ok
            ? { ok: true, data: response.data, message: "" }
            : {
                ok: false,
                data: null,
                status: response.status,
                message: i18n.t(failures[response.status] || "common.error.generic")
            };
    };

    var readFailures = {
        401: "common.error.sessionExpired",
        403: "contact.error.readDenied",
        404: "contact.error.notFound"
    };

    var writeFailures = {
        400: "contact.error.invalid",
        401: "common.error.sessionExpired",
        403: "contact.error.writeDenied",
        404: "contact.error.notFound",
        409: "contact.error.conflict",
        503: "contact.error.retryFailed"
    };

    var submitFailures = {
        400: "contact.error.invalid",
        401: "common.error.sessionExpired",
        404: "contact.error.notFound",
        429: "contact.error.rateLimited"
    };

    var deleteFailures = {
        401: "common.error.sessionExpired",
        403: "contact.error.deleteDenied",
        404: "contact.error.notFound"
    };

    var deletedFailures = {
        401: "common.error.sessionExpired",
        403: "contact.error.deletedDenied",
        404: "contact.error.notDeleted",
        409: "contact.error.conflict"
    };

    var readPath = function (path) {
        return http.get(path).then(function (response) {
            return result(response, readFailures);
        });
    };

    var savePath = function (path, body) {
        return http.post(path, body).then(function (response) {
            return result(response, writeFailures);
        });
    };

    var deletePath = function (path) {
        return http.delete(path).then(function (response) {
            return result(response, deleteFailures);
        });
    };

    var actionPath = function (path, failures) {
        return http.post(path).then(function (response) {
            return result(response, failures);
        });
    };

    var managedPath = function (path, deleted) {
        return path + (deleted ? "?deleted=true" : "");
    };

    return {
        forms: function () {
            return readPath("/contact/forms");
        },

        form: function (id) {
            return readPath("/contact/forms/" + encodeURIComponent(id));
        },

        submit: function (id, fields) {
            return http.post("/contact/forms/" + encodeURIComponent(id) + "/requests", fields).then(function (response) {
                return result(response, submitFailures);
            });
        },

        manageForms: function (deleted) {
            return readPath(managedPath("/contact/manage/forms", deleted));
        },

        manageForm: function (id, deleted) {
            return readPath(managedPath("/contact/manage/forms/" + encodeURIComponent(id), deleted));
        },

        saveForm: function (id, fields) {
            return savePath("/contact/manage/forms" + (id ? "/" + encodeURIComponent(id) : ""), fields);
        },

        removeForm: function (id) {
            return deletePath("/contact/manage/forms/" + encodeURIComponent(id));
        },

        restoreForm: function (id) {
            return actionPath("/contact/manage/forms/" + encodeURIComponent(id) + "/restore", deletedFailures);
        },

        hardDeleteForm: function (id) {
            return deletePath("/contact/manage/forms/" + encodeURIComponent(id) + "/hard");
        },

        manageRequests: function (query) {
            query = query || {};
            return readPath("/contact/manage/requests?" + paging.paging({
                page: query.page || 1,
                pageSize: query.pageSize || 10
            }) + "&search=" + encodeURIComponent(query.search || "")
                + (query.unreadOnly ? "&unreadOnly=true" : "")
                + (query.deleted ? "&deleted=true" : ""));
        },

        request: function (id, deleted) {
            return readPath(managedPath("/contact/manage/requests/" + encodeURIComponent(id), deleted));
        },

        removeRequest: function (id) {
            return deletePath("/contact/manage/requests/" + encodeURIComponent(id));
        },

        restoreRequest: function (id) {
            return actionPath("/contact/manage/requests/" + encodeURIComponent(id) + "/restore", deletedFailures);
        },

        hardDeleteRequest: function (id) {
            return deletePath("/contact/manage/requests/" + encodeURIComponent(id) + "/hard");
        },

        setRequestRead: function (id, read) {
            return savePath("/contact/manage/requests/" + encodeURIComponent(id) + "/read", { read: read });
        },

        retryRequest: function (id) {
            return actionPath("/contact/manage/requests/" + encodeURIComponent(id) + "/retry", writeFailures);
        }
    };
});
