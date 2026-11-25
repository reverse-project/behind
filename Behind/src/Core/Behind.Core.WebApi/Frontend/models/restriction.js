define(["ko", "models/http", "models/session"], function (ko, http, session) {
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
            var owner = session.generation();
            return http.get("/access/restriction").then(function (response) {
                if (response.ok && owner === session.generation()) {
                    title(response.data.title || "");
                    message(response.data.message || "");
                    restricted(!!response.data.restricted);
                }
            });
        }
    };
});
