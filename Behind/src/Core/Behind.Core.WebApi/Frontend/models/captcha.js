define(["models/http"], function (http) {
    return {
        load: function () {
            return http.get("/captcha").then(function (response) {
                return response.ok ? { id: response.data.id, image: response.data.image } : { id: "", image: "" };
            });
        }
    };
});
