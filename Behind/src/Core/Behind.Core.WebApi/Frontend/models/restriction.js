define(["ko", "models/http"], function (ko, http) {
    var restricted = ko.observable(false);
    var message = ko.observable("");
    var title = ko.observable("");

    return {
        restricted: restricted,
        message: message,
        title: title,

        clear: function () {
            title("");
            message("");
            restricted(false);
        },

        load: function () {
            return http.get("/access/restriction").then(function (response) {
                if (response.ok) {
                    title(response.data.title || "");
                    message(response.data.message || "");
                    restricted(!!response.data.restricted);
                }
            });
        }
    };
});
