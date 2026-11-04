define(["models/http", "models/results", "models/i18n"], function (http, results, i18n) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "slug-taken": "downloads.error.slugTaken",
        "parent-deleted": "downloads.error.parentDeleted",
        "folder-deleted": "downloads.error.folderDeleted",
        "version-conflict": "downloads.error.versionConflict"
    });

    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var readFailures = {
        404: "downloads.error.folderGone"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "downloads.error.deletedReadDenied"
    };

    var writeFailures = {
        400: "downloads.error.nameRequired",
        401: sessionExpired,
        403: "downloads.error.writeDenied",
        404: "downloads.error.itemGone",
        409: "downloads.error.notSaved"
    };

    var uploadFailures = {
        400: "downloads.error.chooseFiles",
        401: sessionExpired,
        403: "downloads.error.uploadDenied",
        404: "downloads.error.folderGone",
        413: "downloads.error.filesTooLarge"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "downloads.error.deleteDenied",
        404: "downloads.error.itemGone"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "downloads.error.restoreDenied",
        404: "downloads.error.notDeleted",
        409: "downloads.error.notChanged"
    };

    var fileErrors = {
        empty: "downloads.error.fileEmpty",
        "too-large": "downloads.error.fileTooLarge",
        "folder-not-found": "downloads.error.folderGone",
        "version-conflict": "downloads.error.fileNotSaved"
    };

    var folderDraft = function (fields) {
        return { displayName: fields.displayName, slug: fields.slug };
    };

    var folderPath = function (id) {
        return "/downloads/folders/" + id;
    };

    var filePath = function (id) {
        return "/downloads/files/" + id;
    };

    var post = function (url, body, done, failures) {
        return http.post(url, body).then(function (response) {
            return saved(response, done, failures);
        });
    };

    return {
        folders: function () {
            return http.get("/downloads/folders").then(function (response) {
                return loaded(response, readFailures);
            });
        },

        files: function (folderId) {
            return http.get(folderPath(folderId) + "/files").then(function (response) {
                return loaded(response, readFailures);
            });
        },

        deleted: function () {
            return http.get("/downloads/deleted").then(function (response) {
                return loaded(response, deletedReadFailures);
            });
        },

        createFolder: function (parentId, fields) {
            var url = parentId ? folderPath(parentId) + "/folders" : "/downloads/folders";

            return post(url, folderDraft(fields), "downloads.saved.folderCreated", writeFailures);
        },

        saveFolder: function (id, fields) {
            return post(folderPath(id), folderDraft(fields), "downloads.saved.folderSaved", writeFailures);
        },

        removeFolder: function (id) {
            return post(folderPath(id) + "/delete", undefined, "downloads.saved.folderDeleted", removeFailures);
        },

        restoreFolder: function (id) {
            return post(folderPath(id) + "/restore", undefined, "downloads.saved.folderRestored", deletedActionFailures);
        },

        hardDeleteFolder: function (id) {
            return post(folderPath(id) + "/hard/delete", undefined, "downloads.saved.folderHardDeleted", deletedActionFailures);
        },

        upload: function (folderId, file, displayName) {
            var form = new FormData();

            form.append("files", file);
            form.append("displayNames", displayName);

            return http.postForm(folderPath(folderId) + "/files", form).then(function (response) {
                if (!response.ok) {
                    return { ok: false, message: i18n.t(uploadFailures[response.status] || "downloads.error.uploadFailed") };
                }

                var stored = response.data.files[0];

                return stored.error
                    ? { ok: false, message: i18n.t(fileErrors[stored.error] || "downloads.error.uploadFailed") }
                    : { ok: true, message: "" };
            });
        },

        rename: function (id, displayName) {
            return post(filePath(id), { displayName: displayName }, "downloads.saved.fileRenamed", writeFailures);
        },

        remove: function (id) {
            return post(filePath(id) + "/delete", undefined, "downloads.saved.fileDeleted", removeFailures);
        },

        restore: function (id) {
            return post(filePath(id) + "/restore", undefined, "downloads.saved.fileRestored", deletedActionFailures);
        },

        hardDelete: function (id) {
            return post(filePath(id) + "/hard/delete", undefined, "downloads.saved.fileHardDeleted", deletedActionFailures);
        }
    };
});
