define(["models/http", "models/results"], function (http, results) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "slug-taken": "Another folder in this folder already uses this slug. Choose a different one.",
        "parent-deleted": "The folder this folder belongs to is deleted. Restore that folder first.",
        "folder-deleted": "The folder of this file is deleted. Restore the folder first.",
        "version-conflict": "It was changed at the same time by someone else. Reload the page and try again."
    });

    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var readFailures = {
        404: "This folder no longer exists."
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "You need developer access to see deleted downloads."
    };

    var writeFailures = {
        400: "Enter a display name. The slug can only contain letters, digits and dashes.",
        401: sessionExpired,
        403: "You are not allowed to change downloads.",
        404: "This folder or file no longer exists.",
        409: "It could not be saved."
    };

    var uploadFailures = {
        400: "Choose the files to upload.",
        401: sessionExpired,
        403: "You are not allowed to upload files.",
        404: "This folder no longer exists.",
        413: "The files are too large."
    };

    var removeFailures = {
        401: sessionExpired,
        403: "You are not allowed to delete downloads.",
        404: "This folder or file no longer exists."
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "You need developer access to restore or permanently delete downloads.",
        404: "This folder or file is not deleted.",
        409: "It could not be changed. A deleted folder cannot come back while another folder uses its slug or while the folder it belongs to is deleted."
    };

    var fileErrors = {
        empty: "The file is empty.",
        "too-large": "The file is larger than the allowed maximum.",
        "folder-not-found": "This folder no longer exists.",
        "version-conflict": "The file could not be saved. Try again."
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

            return post(url, folderDraft(fields), "The folder was created.", writeFailures);
        },

        saveFolder: function (id, fields) {
            return post(folderPath(id), folderDraft(fields), "The folder was saved.", writeFailures);
        },

        removeFolder: function (id) {
            return post(folderPath(id) + "/delete", undefined, "The folder was deleted.", removeFailures);
        },

        restoreFolder: function (id) {
            return post(folderPath(id) + "/restore", undefined, "The folder was restored.", deletedActionFailures);
        },

        hardDeleteFolder: function (id) {
            return post(folderPath(id) + "/hard/delete", undefined, "The folder was permanently deleted.", deletedActionFailures);
        },

        upload: function (folderId, file, displayName) {
            var form = new FormData();

            form.append("files", file);
            form.append("displayNames", displayName);

            return http.postForm(folderPath(folderId) + "/files", form).then(function (response) {
                if (!response.ok) {
                    return { ok: false, message: (uploadFailures[response.status] || "The file could not be uploaded.") };
                }

                var stored = response.data.files[0];

                return stored.error
                    ? { ok: false, message: fileErrors[stored.error] || "The file could not be uploaded." }
                    : { ok: true, message: "" };
            });
        },

        rename: function (id, displayName) {
            return post(filePath(id), { displayName: displayName }, "The file was renamed.", writeFailures);
        },

        remove: function (id) {
            return post(filePath(id) + "/delete", undefined, "The file was deleted.", removeFailures);
        },

        restore: function (id) {
            return post(filePath(id) + "/restore", undefined, "The file was restored.", deletedActionFailures);
        },

        hardDelete: function (id) {
            return post(filePath(id) + "/hard/delete", undefined, "The file was permanently deleted.", deletedActionFailures);
        }
    };
});
