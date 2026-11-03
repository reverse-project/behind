define(["models/http"], function (http) {
    var enabledByDefault = { news: true };

    return {
        settings: function () {
            return http.get("/comments/settings").then(function (response) {
                return response.ok ? response.data : enabledByDefault;
            });
        }
    };
});
