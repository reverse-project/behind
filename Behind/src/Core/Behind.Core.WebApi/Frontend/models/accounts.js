define(["ko", "models/http"], function (ko, http) {
    var signIn = ko.observable(true);
    var signUp = ko.observable(true);

    return {
        signIn: signIn,
        signUp: signUp,

        load: function () {
            return http.get("/accounts/settings").then(function (response) {
                if (response.ok) {
                    signIn(response.data.signIn);
                    signUp(response.data.signUp);
                }
            });
        }
    };
});
