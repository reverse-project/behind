define(["ko", "models/http"], function (ko, http) {
    var signIn = ko.observable(true);
    var signUp = ko.observable(true);
    var password = ko.observable(true);
    var magicLink = ko.observable(true);
    var oidc = ko.observable(true);
    var twoFactor = ko.observable(true);
    var passkey = ko.observable(true);

    return {
        signIn: signIn,
        signUp: signUp,
        password: password,
        magicLink: magicLink,
        oidc: oidc,
        twoFactor: twoFactor,
        passkey: passkey,

        load: function () {
            return http.get("/accounts/settings").then(function (response) {
                if (response.ok) {
                    signIn(response.data.signIn);
                    signUp(response.data.signUp);
                    password(response.data.password !== false);
                    magicLink(response.data.magicLink !== false);
                    oidc(response.data.oidc !== false);
                    twoFactor(response.data.twoFactor !== false);
                    passkey(response.data.passkey !== false);
                }
            });
        }
    };
});
