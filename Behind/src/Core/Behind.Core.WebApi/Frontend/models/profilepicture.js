define(["models/http", "models/i18n"], function (http, i18n) {
    var checked = function (response) {
        if (!response.ok) {
            var key = response.status === 413 ? "profile.pictureTooLarge"
                : response.status === 415 ? "profile.pictureInvalid" : "common.error.generic";

            throw new Error(i18n.t(key));
        }

        return response.data;
    };
    return {
        get: function () { return http.get("/profile/picture").then(checked); },
        upload: function (file) {
            var form = new FormData();
            form.append("file", file);
            return http.postForm("/profile/picture", form).then(checked);
        },
        remove: function () { return http.delete("/profile/picture").then(checked); }
    };
});
